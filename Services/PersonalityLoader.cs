using Android.Content;

namespace AlyaOfflineChat.Services;

public static class PersonalityLoader
{
    public static string LoadFromAssets(Context context)
    {
        try
        {
            using var stream = context.Assets.Open("personality.txt");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch
        {
            return "Alya adalah AI companion yang ceria, ramah, dan membantu.";
        }
    }
}
