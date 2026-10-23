using System.IO;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace GainGround.Game
{
    internal static class BrowserStorage
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern string GainGroundStorageGet(string key);
        [DllImport("__Internal")] static extern int GainGroundStorageSet(string key, string value);
        [DllImport("__Internal")] static extern int GainGroundStorageDelete(string key);
#endif

        public static string Read(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var stored = GainGroundStorageGet(key);
            if (stored != null) return stored;
#endif
            // Existing browser saves used PlayerPrefs; keep them readable.
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
        }

        public static void Write(string key, string value)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (GainGroundStorageSet(key, value) == 0 || GainGroundStorageGet(key) != value)
                throw new IOException("O navegador não permitiu salvar. Verifique o armazenamento deste site no Brave.");
#else
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
#endif
        }

        public static void Delete(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (GainGroundStorageDelete(key) == 0)
                throw new IOException("O navegador não permitiu remover este save.");
#endif
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
