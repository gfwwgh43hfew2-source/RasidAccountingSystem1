using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RasidAccountingSystem.Models
{
    public class BackupSettings
    {
        public bool AutoBackupEnabled { get; set; } = false;
        public string BackupPath { get; set; } = "";
        public string BackupFrequency { get; set; } = "OnSave";
        public int BackupRetention { get; set; } = 10;

        /// <summary>
        /// تاريخ ووقت آخر نسخة احتياطية تلقائية مجدولة تم تنفيذها فعلياً (وليس النسخة اليدوية
        /// أو نسخة الإغلاق). تُستخدم لحساب متى تستحق النسخة المجدولة التالية بناءً على BackupFrequency.
        /// </summary>
        public DateTime? LastBackupDate { get; set; }
    }
}