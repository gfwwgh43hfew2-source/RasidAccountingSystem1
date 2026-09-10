using System;
using System.Security.Cryptography;
using System.Text;

namespace RasidAccountingSystem.Helpers
{
    /// <summary>
    /// كلاس مسؤول عن تشفير كلمات المرور والتحقق منها بأمان.
    ///
    /// المعيار الجديد المستخدم: PBKDF2 (Rfc2898DeriveBytes) مع SHA256 و Salt عشوائي مختلف
    /// لكل مستخدم، و100,000 تكرار (Iteration) لجعل أي محاولة لتخمين كلمة المرور بطيئة جداً
    /// حتى لو وصل أحدهم لملف قاعدة البيانات مباشرة.
    ///
    /// الصيغة المخزَّنة في عمود PasswordHash تكون بالشكل التالي (نص واحد ذاتي الوصف):
    ///     PBKDF2$<عدد التكرارات>$<Salt بصيغة Base64>$<الهاش الناتج بصيغة Base64>
    ///
    /// التوافق مع كلمات المرور القديمة:
    /// كانت كلمات المرور قديماً تُخزَّن كـ SHA256 عادي بدون Salt (نص Hex بطول 64 حرف بدون علامة $).
    /// هذا الكلاس يتعرف تلقائياً على الصيغة القديمة، ويتحقق منها بنفس الطريقة القديمة تماماً
    /// (حتى لا تنقطع تسجيلات الدخول لأي مستخدم موجود مسبقاً)، ثم تقوم طبقة قاعدة البيانات
    /// (DatabaseService.ValidateUserAsync) بترقية كلمة المرور تلقائياً وبصمت للصيغة الجديدة
    /// الآمنة بمجرد أن يسجّل المستخدم دخوله بنجاح مرة واحدة - بدون أي تدخل أو إزعاج للمستخدم.
    /// </summary>
    public static class PasswordHelper
    {
        private const string Pbkdf2Prefix = "PBKDF2";
        private const int DefaultIterations = 100000;
        private const int SaltSizeBytes = 16;
        private const int HashSizeBytes = 32;

        /// <summary>
        /// تشفير كلمة مرور جديدة بالمعيار الآمن (PBKDF2 + Salt عشوائي).
        /// يُستخدم عند إنشاء مستخدم جديد أو تغيير كلمة مرور.
        /// </summary>
        public static string HashPassword(string password)
        {
            if (password == null)
                throw new ArgumentNullException(nameof(password));

            byte[] salt = new byte[SaltSizeBytes];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            byte[] hashBytes;
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, DefaultIterations, HashAlgorithmName.SHA256))
            {
                hashBytes = pbkdf2.GetBytes(HashSizeBytes);
            }

            return $"{Pbkdf2Prefix}${DefaultIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hashBytes)}";
        }

        /// <summary>
        /// التحقق من كلمة مرور مقابل الهاش المخزَّن في قاعدة البيانات.
        /// يدعم تلقائياً كل من الصيغة الجديدة (PBKDF2) والصيغة القديمة (SHA256 بدون Salt).
        /// </summary>
        public static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash) || password == null)
                return false;

            return IsPbkdf2Hash(storedHash)
                ? VerifyPbkdf2Hash(password, storedHash)
                : VerifyLegacySha256Hash(password, storedHash);
        }

        /// <summary>
        /// هل الهاش المخزَّن بالصيغة القديمة غير الآمنة (SHA256 بدون Salt)؟
        /// تُستخدم هذه الدالة لتحديد هل يجب ترقية كلمة المرور تلقائياً بعد نجاح تسجيل الدخول.
        /// </summary>
        public static bool IsLegacyHash(string storedHash)
        {
            return !string.IsNullOrEmpty(storedHash) && !IsPbkdf2Hash(storedHash);
        }

        private static bool IsPbkdf2Hash(string storedHash)
        {
            return storedHash.StartsWith(Pbkdf2Prefix + "$", StringComparison.Ordinal);
        }

        private static bool VerifyPbkdf2Hash(string password, string storedHash)
        {
            try
            {
                string[] parts = storedHash.Split('$');

                // الصيغة المتوقعة: PBKDF2 $ التكرارات $ الملح $ الهاش  => 4 أجزاء
                if (parts.Length != 4)
                    return false;

                int iterations = int.Parse(parts[1]);
                byte[] salt = Convert.FromBase64String(parts[2]);
                byte[] expectedHash = Convert.FromBase64String(parts[3]);

                byte[] actualHash;
                using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
                {
                    actualHash = pbkdf2.GetBytes(expectedHash.Length);
                }

                return FixedTimeEquals(actualHash, expectedHash);
            }
            catch
            {
                // أي خطأ في تحليل صيغة الهاش يعني أن التحقق يفشل بأمان (وليس Exception يوقف تسجيل الدخول)
                return false;
            }
        }

        private static bool VerifyLegacySha256Hash(string password, string storedHash)
        {
            try
            {
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                    var sb = new StringBuilder();
                    foreach (byte b in bytes)
                        sb.Append(b.ToString("x2"));

                    return string.Equals(sb.ToString(), storedHash, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// مقارنة مصفوفتَي بايت بزمن ثابت لا يعتمد على محتواهما، لمنع هجمات "قياس زمن الاستجابة"
        /// (Timing Attacks) التي قد تكشف أجزاءً من الهاش الصحيح تدريجياً.
        /// </summary>
        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }

            return diff == 0;
        }
    }
}
