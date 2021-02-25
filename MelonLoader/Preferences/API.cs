﻿using System;
using System.Collections.Generic;
using System.IO;

namespace MelonLoader
{
    public static class MelonPreferences
    {
        public static readonly List<MelonPreferences_Category> Categories = new List<MelonPreferences_Category>();
        public static readonly TomlMapper Mapper = new TomlMapper();
        internal static List<Preferences.IO.File> PrefFiles = new List<Preferences.IO.File>();
        internal static Preferences.IO.File DefaultFile = null;

        static MelonPreferences() => DefaultFile = new Preferences.IO.File(
            Path.Combine(MelonUtils.UserDataDirectory, "MelonPreferences.cfg"), 
            Path.Combine(MelonUtils.UserDataDirectory, "modprefs.ini"));

        public static void Load()
        {
            try
            {
                DefaultFile.LegacyLoad();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error while Loading Legacy Preferences from {DefaultFile.LegacyFilePath}: {ex}");
                DefaultFile.WasError = true;
            }

            try
            {
                DefaultFile.Load();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error while Loading Preferences from {DefaultFile.FilePath}: {ex}");
                DefaultFile.WasError = true;
            }

            if (PrefFiles.Count >= 0)
            {
                foreach (Preferences.IO.File file in PrefFiles)
                {
                    try
                    {
                        file.Load();
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"Error while Loading Preferences from {file.FilePath}: {ex}");
                        file.WasError = true;
                    }
                }
            }

            if (Categories.Count > 0)
            {
                foreach (MelonPreferences_Category category in Categories)
                {
                    if (category.Entries.Count < 0)
                        continue;
                    Preferences.IO.File currentFile = category.File;
                    if (currentFile == null)
                        currentFile = DefaultFile;
                    if (currentFile.WasError)
                        continue;
                    foreach (MelonPreferences_Entry entry in category.Entries)
                        currentFile.SetupEntryFromRawValue(entry);
                }
            }

            MelonLogger.Msg("Preferences Loaded!");
            MelonHandler.OnPreferencesLoaded();
        }

        public static void Save()
        {
            foreach (MelonPreferences_Category category in Categories)
            {
                Preferences.IO.File currentFile = category.File;
                if (currentFile == null)
                    currentFile = DefaultFile;
                foreach (MelonPreferences_Entry entry in category.Entries)
                    if (!(entry.DontSaveDefault && entry.GetValueAsString() == entry.GetDefaultValueAsString()))
                        currentFile.SetupRawValue(category.Identifier, entry.Identifier, entry.Save());
            }

            try
            {
                DefaultFile.Save();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error while Saving Preferences to {DefaultFile.FilePath}: {ex}");
                DefaultFile.WasError = true;
            }

            if (PrefFiles.Count >= 0)
            {
                foreach (Preferences.IO.File file in PrefFiles)
                {
                    try
                    {
                        file.Save();
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"Error while Saving Preferences to {file.FilePath}: {ex}");
                        file.WasError = true;
                    }
                }
            }

            MelonLogger.Msg("Preferences Saved!");
            MelonHandler.OnPreferencesSaved();
        }

        public static MelonPreferences_Category CreateCategory(string identifier, string display_name = null)
        {
            if (string.IsNullOrEmpty(identifier))
                throw new Exception("identifier is null or empty when calling CreateCategory");
            if (display_name == null)
                display_name = identifier;
            MelonPreferences_Category category = GetCategory(identifier);
            if (category != null)
                return category;
            return new MelonPreferences_Category(identifier, display_name);
        }

        [Obsolete]
        public static MelonPreferences_Entry CreateEntry<T>(string category_identifier, string entry_identifier,
            T default_value, string display_name, bool is_hidden) => CreateEntry(category_identifier,
            entry_identifier, default_value, display_name, is_hidden, false);
        
        public static MelonPreferences_Entry<T> CreateEntry<T>(string category_identifier, string entry_identifier, T default_value, string display_name = null, bool is_hidden = false, bool dont_save_default = false)
        {
            if (string.IsNullOrEmpty(category_identifier))
                throw new Exception("category_identifier is null or empty when calling CreateEntry");
            if (string.IsNullOrEmpty(entry_identifier))
                throw new Exception("entry_identifier is null or empty when calling CreateEntry");
            MelonPreferences_Category category = GetCategory(entry_identifier);
            if (category == null)
                category = CreateCategory(category_identifier);
            return category.CreateEntry(entry_identifier, default_value, display_name, is_hidden, dont_save_default);
        }

        public static MelonPreferences_Category GetCategory(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                throw new Exception("identifier is null or empty when calling GetCategory");
            if (Categories.Count <= 0)
                return null;
            return Categories.Find(x => x.Identifier.Equals(identifier));
        }
        public static MelonPreferences_Entry GetEntry(string category_identifier, string entry_identifier) => GetCategory(category_identifier)?.GetEntry(entry_identifier);
        public static MelonPreferences_Entry<T> GetEntry<T>(string category_identifier, string entry_identifier) => GetCategory(category_identifier)?.GetEntry<T>(entry_identifier);
        public static bool HasEntry(string category_identifier, string entry_identifier) => (GetEntry(category_identifier, entry_identifier) != null);
        public static void SetEntryValue<T>(string category_identifier, string entry_identifier, T value)
        {
            var entry = GetCategory(category_identifier)?.GetEntry<T>(entry_identifier);
            if (entry != null) entry.Value = value;
        }

        public static T GetEntryValue<T>(string category_identifier, string entry_identifier)
        {
            MelonPreferences_Category cat = GetCategory(category_identifier);
            if (cat == null)
                return default;
            var entry = cat.GetEntry<T>(entry_identifier);
            if (entry == null)
                return default;
            return entry.Value;
        }
        
        internal static Preferences.IO.File GetPrefFileFromFilePath(string filepath)
        {
            if (PrefFiles.Count <= 0)
                return null;
            FileInfo filepathinfo = new FileInfo(filepath);
            foreach (Preferences.IO.File file in PrefFiles)
            {
                FileInfo filepathinfo2 = new FileInfo(file.FilePath);
                if (filepathinfo.FullName.Equals(filepathinfo2.FullName))
                    return file;
            }
            return null;
        }

        internal static bool IsFileInUse(Preferences.IO.File file)
        {
            if (Categories.Count <= 0)
                return false;
            if (file == null)
                file = DefaultFile;
            foreach (MelonPreferences_Category category in Categories)
            {
                Preferences.IO.File currentFile = category.File;
                if (currentFile == null)
                    currentFile = DefaultFile;
                if (currentFile == file)
                    return true;
            }
            return false;
        }

        internal static void LoadFileAndRefreshCategories(Preferences.IO.File file)
        {
            try
            {
                file.Load();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error while Loading Preferences from {file.FilePath}: {ex}");
                file.WasError = true;
            }
            if (file.WasError || (Categories.Count <= 0))
                return;
            foreach (MelonPreferences_Category category in Categories)
            {
                Preferences.IO.File currentFile = category.File;
                if (currentFile == null)
                    currentFile = DefaultFile;
                if ((currentFile != file) || (category.Entries.Count <= 0))
                    continue;
                foreach (MelonPreferences_Entry entry in category.Entries)
                    currentFile.SetupEntryFromRawValue(entry);
            }
        }
    }
}
