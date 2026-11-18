using System;
using System.Collections.Generic;

namespace ClickCircle
{
    public sealed class SubjectHistoryEntry
    {
        public string Subject { get; set; }
        public bool Pinned { get; set; }
    }

    public sealed class SubjectHistory
    {
        public const int RecentLimit = 50;
        public const int PinnedLimit = 50;
        private readonly List<SubjectHistoryEntry> entries = new List<SubjectHistoryEntry>();

        public SubjectHistory() { }

        public SubjectHistory(IEnumerable<SubjectHistoryEntry> saved)
        {
            if (saved == null) return;
            foreach (SubjectHistoryEntry entry in saved)
            {
                if (entry == null || String.IsNullOrWhiteSpace(entry.Subject)) continue;
                string subject = entry.Subject.Trim();
                if (Find(subject) != null) continue;
                entries.Add(new SubjectHistoryEntry { Subject = subject, Pinned = entry.Pinned });
            }
            Trim();
        }

        public bool Remember(string subject)
        {
            if (String.IsNullOrWhiteSpace(subject)) return false;
            subject = subject.Trim();
            SubjectHistoryEntry entry = Find(subject);
            if (entry != null && entries[0] == entry) return false;
            if (entry != null) entries.Remove(entry);
            else entry = new SubjectHistoryEntry { Subject = subject };
            entries.Insert(0, entry);
            Trim();
            return true;
        }

        public bool SetPinned(string subject, bool pinned)
        {
            SubjectHistoryEntry entry = Find(subject);
            if (entry == null || entry.Pinned == pinned) return false;
            if (pinned && entries.FindAll(delegate(SubjectHistoryEntry item) { return item.Pinned; }).Count
                >= PinnedLimit) return false;
            entry.Pinned = pinned;
            Trim();
            return true;
        }

        public List<SubjectHistoryEntry> Search(string query)
        {
            string filter = (query ?? String.Empty).Trim();
            List<SubjectHistoryEntry> result = new List<SubjectHistoryEntry>();
            foreach (bool pinned in new[] { true, false })
                foreach (SubjectHistoryEntry entry in entries)
                    if (entry.Pinned == pinned &&
                        entry.Subject.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                        result.Add(Copy(entry));
            return result;
        }

        public List<SubjectHistoryEntry> Export()
        {
            return entries.ConvertAll<SubjectHistoryEntry>(Copy);
        }

        private SubjectHistoryEntry Find(string subject)
        {
            return entries.Find(delegate(SubjectHistoryEntry entry) {
                return String.Equals(entry.Subject, subject, StringComparison.Ordinal);
            });
        }

        private void Trim()
        {
            int recent = 0, pinned = 0;
            entries.RemoveAll(delegate(SubjectHistoryEntry entry) {
                return entry.Pinned ? ++pinned > PinnedLimit : ++recent > RecentLimit;
            });
        }

        private static SubjectHistoryEntry Copy(SubjectHistoryEntry entry)
        {
            return new SubjectHistoryEntry { Subject = entry.Subject, Pinned = entry.Pinned };
        }
    }
}
