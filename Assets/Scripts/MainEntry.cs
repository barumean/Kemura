using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using MinorShift.Emuera;
using MinorShift._Library;
using System.Text;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

public class MainEntry : MonoBehaviour
{
    void Awake()
    {
        Application.targetFrameRate = 24;
        ResolutionHelper.Apply();
    }

    void Start()
    {
        LoadConfigMaps();
        if(!MultiLanguage.SetLanguage())
        {
            Object.FindObjectOfType<OptionWindow>().ShowLanguageBox();
        }

        FirstWindow.Show();

#if UNITY_EDITOR
        uEmuera.Logger.info = GenericUtils.Info;
        uEmuera.Logger.warn = GenericUtils.Warn;
        uEmuera.Logger.error = GenericUtils.Error;
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        RequestStoragePermissionsIfNeeded();
#endif
    }

#if UNITY_EDITOR
    public string era_path;
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
    static void RequestStoragePermissionsIfNeeded()
    {
        int sdk = AndroidVersionHelper.SdkInt;
        // Android 6~9: READ/WRITE 런타임 권한 요청
        if (sdk >= 23 && sdk < 30)
        {
            if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageRead))
                Permission.RequestUserPermission(Permission.ExternalStorageRead);
            if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageWrite))
                Permission.RequestUserPermission(Permission.ExternalStorageWrite);
        }
        // Android 10: requestLegacyExternalStorage 플래그로 처리
        // Android 11+: MANAGE_EXTERNAL_STORAGE는 FirstWindow에서 별도 처리
    }
#endif

    void LoadConfigMaps()
    {
        char[] split = new char[] { '\x0d', '\x0a' };
        var shiftjis = Resources.Load<TextAsset>("Text/emuera_config_shiftjis");
        if(shiftjis == null)
            return;
        var utf8 = Resources.Load<TextAsset>("Text/emuera_config_utf8");
        if(utf8 == null)
            return;
        var utf8_cn = Resources.Load<TextAsset>("Text/emuera_config_utf8_zhcn");
        if(utf8_cn == null)
            return;

        var jis_bytes = shiftjis.bytes;
        var jis_md5_strs = GenericUtils.CalcMd5List(jis_bytes);

        var utf8_strs = utf8.text.Split(split);
        var utf8_str_list = new List<string>();
        foreach (var str in utf8_strs)
        {
            if (string.IsNullOrWhiteSpace(str))
                continue;
            utf8_str_list.Add(str);
        }

        var utf8cn_strs = utf8_cn.text.Split(split);
        var utf8cn_str_list = new List<string>();
        foreach (var str in utf8cn_strs)
        {
            if (string.IsNullOrWhiteSpace(str))
                continue;
            utf8cn_str_list.Add(str);
        }

        if (jis_md5_strs.Count != utf8cn_str_list.Count)
            return;

        Dictionary<string, string> jis_map = new Dictionary<string, string>();
        for(int i = 0; i < jis_md5_strs.Count; ++i)
        {
            jis_map[jis_md5_strs[i]] = utf8_str_list[i];
        }
        Dictionary<string, string> utf8cn_map = new Dictionary<string, string>();
        for(int i = 0; i < utf8cn_str_list.Count; ++i)
        {
            utf8cn_map[utf8cn_str_list[i]] = utf8_str_list[i];
        }
        uEmuera.Utils.SetSHIFTJIS_to_UTF8Dict(jis_map);
        uEmuera.Utils.SetUTF8ZHCN_to_UTF8Dict(utf8cn_map);
    }
}
