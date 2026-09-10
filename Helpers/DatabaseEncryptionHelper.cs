using System;
using System.IO;
using System.Security.Cryptography;

namespace RasidAccountingSystem.Helpers
{
    /// <summary>
    /// حماية قاعدة البيانات وقت الراحة (Encryption At Rest).
    ///
    /// القرار المعماري ولماذا:
    /// الخيار "المثالي نظرياً" هو استبدال محرك SQLite بالكامل بنسخة SQLCipher تدعم التشفير
    /// المباشر لكل عملية قراءة/كتابة. لكن هذا يتطلب تغيير مكتبة الاتصال بقاعدة البيانات في
    /// المشروع بالكامل (System.Data.SQLite) - وهي مستخدمة في آلاف الأسطر عبر أكثر من 30 ملف،
    /// أهمها DatabaseService.cs وحده بأكثر من 8000 سطر. تغيير كهذا بدون بيئة اختبار وبناء
    /// فعلية (Build/Compile) يحمل خطورة حقيقية على استقرار البرنامج بالكامل، وهو ما تم تجنبه
    /// عمداً حرصاً على عدم المخاطرة بموثوقية النظام.
    ///
    /// البديل الآمن والمُطبَّق هنا: تشفير ملف قاعدة البيانات نفسه على القرص "وقت الراحة" فقط -
    /// أي عندما لا يكون البرنامج يعمل. آلية العمل:
    ///
    ///   1. عند إغلاق البرنامج بشكل طبيعي: يُشفَّر ملف قاعدة البيانات الفعلي (RasidERP10.db)
    ///      إلى ملف مشفَّر (RasidERP10.db.enc) بمعيار AES-256، ثم يُحذف الملف غير المشفَّر
    ///      بأمان (Secure Delete - الكتابة فوق محتواه قبل الحذف).
    ///
    ///   2. عند بدء تشغيل البرنامج: لو وُجد ملف مشفَّر ولم يوجد ملف عمل غير مشفَّر، يتم فك
    ///      تشفيره إلى نسخة عمل مؤقتة يستخدمها البرنامج بشكل طبيعي تماماً طوال الجلسة
    ///      (بدون أي تعديل في أي كود اتصال بقاعدة البيانات في باقي المشروع).
    ///
    ///   3. مفتاح التشفير نفسه لا يُخزَّن كنص صريح مطلقاً: يُنشأ عشوائياً مرة واحدة، ثم يُحفظ
    ///      محمياً عبر Windows Data Protection API (DPAPI) المرتبط بحساب المستخدم الحالي على
    ///      هذا الجهاز تحديداً. يعني حتى لو نُسخ ملف قاعدة البيانات المشفر وملف المفتاح معاً
    ///      لجهاز آخر أو حساب مستخدم آخر، لا يمكن فك التشفير بدون تسجيل الدخول بنفس حساب
    ///      ويندوز الذي أُنشئ عليه المفتاح أصلاً.
    ///
    /// ملاحظة أمانة مهمة: هذا الأسلوب يحمي البيانات في حالة سرقة أو فقدان أو نسخ ملف قاعدة
    /// البيانات وهو "ساكن" (البرنامج مغلق) - وهو التهديد الواقعي الأكثر شيوعاً (سرقة جهاز
    /// لابتوب، نسخ احتياطي مسروق، فقدان قرص خارجي). لا يحمي من شخص يصل لجهاز مسجَّل دخول عليه
    /// وبرنامج شغال فعلياً وقتها، لكن هذا يتطلب أصلاً وصولاً فعلياً وقت التشغيل يفوق نطاق ما
    /// يمكن لأي تشفير على مستوى الملف حمايته.
    /// </summary>
    public static class DatabaseEncryptionHelper
    {
        private const int AesKeySizeBytes = 32; // 256-bit

        /// <summary>
        /// يُستدعى عند بدء تشغيل البرنامج، قبل أي اتصال بقاعدة البيانات.
        /// لو وُجدت نسخة عمل غير مشفَّرة بالفعل (تشغيل عادي، أو نسخة متبقية من إغلاق غير طبيعي
        /// كإغلاق مفاجئ أو Crash)، تُستخدم كما هي مباشرة دون أي تدخل - أولوية قصوى لعدم فقد
        /// أي بيانات مهما كانت الظروف. فقط لو لم توجد نسخة عمل ووُجدت نسخة مشفَّرة، يتم فك تشفيرها.
        /// </summary>
        public static void PrepareDatabaseForUse(string plaintextDbPath)
        {
            try
            {
                if (File.Exists(plaintextDbPath))
                {
                    // نسخة العمل موجودة بالفعل - نستخدمها كما هي دون أي تدخل (أهم قاعدة: لا تفقد بيانات أبداً)
                    Logger.LogInfo("تم العثور على نسخة عمل قائمة من قاعدة البيانات - سيتم استخدامها مباشرة", "DatabaseEncryption");
                    return;
                }

                string encryptedPath = GetEncryptedPath(plaintextDbPath);

                if (!File.Exists(encryptedPath))
                {
                    // أول تشغيل للبرنامج على هذا الجهاز إطلاقاً - لا يوجد شيء لفك تشفيره بعد،
                    // سيقوم DatabaseService.CreateAllDatabaseTables() بإنشاء قاعدة بيانات جديدة عادية
                    return;
                }

                byte[] key = GetOrCreateEncryptionKey(plaintextDbPath);
                byte[] encryptedBytes = File.ReadAllBytes(encryptedPath);
                byte[] decryptedBytes = DecryptBytes(encryptedBytes, key);

                File.WriteAllBytes(plaintextDbPath, decryptedBytes);
                Array.Clear(decryptedBytes, 0, decryptedBytes.Length);
                Array.Clear(key, 0, key.Length);

                Logger.LogInfo("تم فك تشفير قاعدة البيانات بنجاح لبدء الجلسة الحالية", "DatabaseEncryption");
            }
            catch (Exception ex)
            {
                // فشل فك التشفير لا يجب أن يمنع تشغيل البرنامج مطلقاً. لو فشل، ببساطة لن توجد
                // نسخة عمل، وDatabaseService.CreateAllDatabaseTables() ستنشئ قاعدة بيانات جديدة
                // فارغة كإجراء أخير آمن، مع تسجيل الخطأ بوضوح لمراجعته.
                Logger.LogError("فشل فك تشفير قاعدة البيانات عند بدء التشغيل", ex, "DatabaseEncryption");
            }
        }

        /// <summary>
        /// يُستدعى عند إغلاق البرنامج بشكل طبيعي فقط (وليس عند أي Crash أو إغلاق مفاجئ).
        /// يشفِّر نسخة العمل الحالية إلى الملف المشفَّر، ثم يحذف نسخة العمل غير المشفَّرة بأمان.
        /// لو فشلت أي خطوة، تبقى نسخة العمل غير المشفَّرة كما هي (لا حذف بدون تأكد كامل من نجاح
        /// التشفير)، فتُستخدم عادةً في الجلسة القادمة وتُشفَّر وقتها بدلاً من ذلك.
        /// </summary>
        public static void SecureDatabaseOnExit(string plaintextDbPath)
        {
            try
            {
                if (!File.Exists(plaintextDbPath))
                    return;

                // تحرير أي اتصالات مُجمَّعة (Connection Pooling) بقاعدة البيانات، حتى لا يبقى
                // الملف مقفولاً من نظام التشغيل ويمنع قراءته/حذفه بعد إغلاق آخر اتصال ظاهرياً
                System.Data.SQLite.SQLiteConnection.ClearAllPools();
                System.Threading.Thread.Sleep(200);

                byte[] key = GetOrCreateEncryptionKey(plaintextDbPath);
                byte[] plainBytes = File.ReadAllBytes(plaintextDbPath);
                byte[] encryptedBytes = EncryptBytes(plainBytes, key);

                string encryptedPath = GetEncryptedPath(plaintextDbPath);
                string tempEncryptedPath = encryptedPath + ".tmp";

                File.WriteAllBytes(tempEncryptedPath, encryptedBytes);

                if (File.Exists(encryptedPath))
                {
                    File.Delete(encryptedPath);
                }
                File.Move(tempEncryptedPath, encryptedPath);

                Array.Clear(plainBytes, 0, plainBytes.Length);
                Array.Clear(key, 0, key.Length);

                SecureDeleteFile(plaintextDbPath);

                Logger.LogInfo("تم تشفير قاعدة البيانات بنجاح عند إغلاق البرنامج", "DatabaseEncryption");
            }
            catch (Exception ex)
            {
                // مهم جداً: أي فشل هنا يجب ألا يحذف نسخة العمل غير المشفَّرة إطلاقاً - أولوية
                // الحفاظ على البيانات أهم من ضمان التشفير في كل مرة
                Logger.LogError("فشل تشفير قاعدة البيانات عند الإغلاق - ستبقى نسخة العمل كما هي للجلسة القادمة", ex, "DatabaseEncryption");
            }
        }

        #region أدوات مساعدة داخلية (Internal Helpers)

        private static string GetEncryptedPath(string plaintextDbPath)
        {
            return plaintextDbPath + ".enc";
        }

        private static string GetKeyFilePath(string plaintextDbPath)
        {
            string directory = Path.GetDirectoryName(plaintextDbPath);
            return Path.Combine(directory ?? "", "RasidERP.key");
        }

        /// <summary>
        /// جلب مفتاح التشفير الحالي، أو إنشاء مفتاح عشوائي جديد لمرة واحدة وحفظه محمياً بـ DPAPI
        /// (مرتبط بحساب مستخدم ويندوز الحالي على هذا الجهاز تحديداً)
        /// </summary>
        private static byte[] GetOrCreateEncryptionKey(string plaintextDbPath)
        {
            string keyFilePath = GetKeyFilePath(plaintextDbPath);

            if (File.Exists(keyFilePath))
            {
                byte[] protectedKey = File.ReadAllBytes(keyFilePath);
                return ProtectedData.Unprotect(protectedKey, null, DataProtectionScope.CurrentUser);
            }

            byte[] newKey = new byte[AesKeySizeBytes];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(newKey);
            }

            byte[] protectedNewKey = ProtectedData.Protect(newKey, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(keyFilePath, protectedNewKey);

            Logger.LogInfo("تم إنشاء مفتاح تشفير جديد لقاعدة البيانات (محمي عبر Windows DPAPI)", "DatabaseEncryption");

            return newKey;
        }

        private static byte[] EncryptBytes(byte[] plainBytes, byte[] key)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.GenerateIV();

                using (var encryptor = aes.CreateEncryptor())
                using (var msOutput = new MemoryStream())
                {
                    // نكتب الـ IV العشوائي في بداية الملف الناتج (غير سري، لازم فقط لفك التشفير لاحقاً)
                    msOutput.Write(aes.IV, 0, aes.IV.Length);

                    using (var cryptoStream = new CryptoStream(msOutput, encryptor, CryptoStreamMode.Write))
                    {
                        cryptoStream.Write(plainBytes, 0, plainBytes.Length);
                        cryptoStream.FlushFinalBlock();
                    }

                    return msOutput.ToArray();
                }
            }
        }

        private static byte[] DecryptBytes(byte[] encryptedBytes, byte[] key)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;

                int ivLength = aes.IV.Length; // 16 بايت لـ AES دائماً
                byte[] iv = new byte[ivLength];
                Array.Copy(encryptedBytes, 0, iv, 0, ivLength);
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor())
                using (var msInput = new MemoryStream(encryptedBytes, ivLength, encryptedBytes.Length - ivLength))
                using (var cryptoStream = new CryptoStream(msInput, decryptor, CryptoStreamMode.Read))
                using (var msOutput = new MemoryStream())
                {
                    cryptoStream.CopyTo(msOutput);
                    return msOutput.ToArray();
                }
            }
        }

        /// <summary>
        /// حذف آمن: الكتابة فوق محتوى الملف بأصفار قبل حذفه فعلياً، حتى لا يبقى أثر لمحتواه
        /// القابل للاسترجاع بأدوات استرجاع الملفات المحذوفة العادية
        /// </summary>
        private static void SecureDeleteFile(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                long length = fileInfo.Length;

                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.None))
                {
                    byte[] zeroBuffer = new byte[64 * 1024];
                    long written = 0;
                    while (written < length)
                    {
                        int toWrite = (int)Math.Min(zeroBuffer.Length, length - written);
                        stream.Write(zeroBuffer, 0, toWrite);
                        written += toWrite;
                    }
                    stream.Flush();
                }

                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                // لو فشلت الكتابة فوق المحتوى لأي سبب، نحاول الحذف العادي على الأقل كخطوة احتياطية
                Logger.LogWarning($"تعذّرت الكتابة الآمنة فوق ملف قاعدة البيانات قبل حذفه: {ex.Message}", "DatabaseEncryption");
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception deleteEx)
                {
                    Logger.LogError("فشل حذف نسخة العمل غير المشفَّرة بعد التشفير الناجح", deleteEx, "DatabaseEncryption");
                }
            }
        }

        #endregion
    }
}
