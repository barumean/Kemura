// Android 버전별 스토리지 권한 처리 헬퍼
using UnityEngine;

public static class AndroidVersionHelper
{
    public static int SdkInt
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                return version.GetStatic<int>("SDK_INT");
#else
            return 0;
#endif
        }
    }

    // Android 11+ (API 30+): MANAGE_EXTERNAL_STORAGE 권한 확인
    public static bool HasManageExternalStorage()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (SdkInt < 30)
            return true;
        using (var environment = new AndroidJavaClass("android.os.Environment"))
            return environment.CallStatic<bool>("isExternalStorageManager");
#else
        return true;
#endif
    }

    // Android 11+ (API 30+): MANAGE_EXTERNAL_STORAGE 권한 요청 (설정 화면으로 이동)
    public static void RequestManageExternalStorage()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (SdkInt < 30)
            return;
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intentClass = new AndroidJavaClass("android.content.Intent"))
            using (var uriClass = new AndroidJavaClass("android.net.Uri"))
            {
                var actionManage = "android.settings.MANAGE_APP_ALL_FILES_ACCESS_PERMISSION";
                var packageName = Application.identifier;
                var uri = uriClass.CallStatic<AndroidJavaObject>("parse", "package:" + packageName);
                using (var intent = new AndroidJavaObject("android.content.Intent", actionManage, uri))
                    activity.Call("startActivity", intent);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("MANAGE_EXTERNAL_STORAGE 요청 실패: " + e.Message);
        }
#endif
    }

    // 외부 스토리지 루트 경로 반환 (예: /storage/emulated/0)
    public static string GetExternalStorageDirectory()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var environment = new AndroidJavaClass("android.os.Environment"))
            using (var file = environment.CallStatic<AndroidJavaObject>("getExternalStorageDirectory"))
                return file.Call<string>("getAbsolutePath");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("외부 스토리지 경로 가져오기 실패: " + e.Message);
        }
#endif
        return null;
    }
}
