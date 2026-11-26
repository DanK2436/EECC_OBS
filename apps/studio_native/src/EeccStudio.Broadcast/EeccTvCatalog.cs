using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EeccStudio.Broadcast;

/// <summary>
/// Même projet Supabase que l'application EECC TV.
/// La clé publiable est celle déjà livrée dans l'application. La clé service, si elle est définie, reste prioritaire.
/// </summary>
public static class EeccCloud
{
    public const string ProjectUrl = "https://rwojdiomftillhsamjal.supabase.co";
    public const string PublishableKey = "sb_publishable_GjqG64b82uXaDQzGJmo3Aw_pIcpjxKb";

    public static (string Url, string Key) Resolve()
    {
        var url = Environment.GetEnvironmentVariable("EECC_SUPABASE_URL");
        var service = Environment.GetEnvironmentVariable("EECC_SUPABASE_SERVICE_KEY");
        if (!string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(service))
            return (url.TrimEnd('/'), service.Trim());

        var anon = Environment.GetEnvironmentVariable("EECC_SUPABASE_ANON_KEY");
        return (string.IsNullOrWhiteSpace(url) ? ProjectUrl : url.TrimEnd('/'),
            string.IsNullOrWhiteSpace(anon) ? PublishableKey : anon.Trim());
    }
}

/// <summary>
/// Écrit le direct dans Supabase pour qu'EECC TV l'affiche.
/// </summary>
public static class EeccTvCatalog
{
    public static async Task<string> PublishLiveAsync(string playbackUrl, string title, string speaker = "", CancellationToken cancellationToken = default)
    {
        var playback = playbackUrl;
        var note = "";
        if (playback.Contains("/live/", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var ready = await PublicProgramRelay.Shared.StartAsync(playback, cancellationToken);
                if (ready)
                {
                    playback = PublicProgramRelay.PlaybackUrl;
                    note = " Les téléphones hors du Wi-Fi lisent le flux public.";
                }
                else
                {
                    note = " " + PublicProgramRelay.Shared.Detail;
                }
            }
            catch (Exception ex)
            {
                note = " Le flux public n'a pas démarré (" + ex.GetType().Name + ").";
            }
        }

        var (url, key) = EeccCloud.Resolve();
        return await SendAsync(url, key, title, speaker, playback, live: true, cancellationToken) + note;
    }

    public static async Task UpdateCaptionAsync(string title, string speaker, string theme, CancellationToken cancellationToken = default)
    {
        var (url, key) = EeccCloud.Resolve();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        http.DefaultRequestHeaders.Add("apikey", key);
        using var request = new HttpRequestMessage(HttpMethod.Post, url + "/rest/v1/rpc/studio_set_live_caption")
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                p_title = title,
                p_speaker = speaker,
                p_theme = theme,
                p_secret = StudioSecret.Current,
            }), Encoding.UTF8, "application/json"),
        };
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Supabase " + (int)response.StatusCode);
    }

    public static async Task<string> EndLiveAsync(CancellationToken cancellationToken = default)
    {
        PublicProgramRelay.Shared.Stop();
        var (url, key) = EeccCloud.Resolve();
        return await SendAsync(url, key, "", "", "", live: false, cancellationToken);
    }

    private static async Task<string> SendAsync(string baseUrl, string key, string title, string speaker, string playbackUrl, bool live, CancellationToken cancellationToken)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
            http.DefaultRequestHeaders.Add("apikey", key);
            var request = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/rest/v1/rpc/publish_studio_live")
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    p_title = title,
                    p_speaker = speaker,
                    p_url = playbackUrl,
                    p_live = live,
                    p_secret = StudioSecret.Current,
                }), Encoding.UTF8, "application/json"),
            };
            var response = await http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
                return live
                    ? "Le direct est publié dans Supabase. EECC TV peut l'afficher."
                    : "Le direct est terminé dans Supabase.";

            return "Supabase a refusé la publication (" + (int)response.StatusCode + "). "
                + Trim(body)
                + " Appliquez la migration studio_live si la fonction publish_studio_live n'existe pas encore.";
        }
        catch (Exception ex)
        {
            return "Supabase n'a pas répondu (" + ex.GetType().Name + ").";
        }
    }

    private static string Trim(string body) =>
        body.Length <= 180 ? body : body[..180];
}
