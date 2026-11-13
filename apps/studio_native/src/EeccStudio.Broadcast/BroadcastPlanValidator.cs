namespace EeccStudio.Broadcast;

public sealed class ValidationResult
{
    private ValidationResult(bool isValid, IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
    {
        IsValid = isValid;
        Errors = errors;
        Warnings = warnings;
    }

    public bool IsValid { get; }

    public IReadOnlyList<string> Errors { get; }

    public IReadOnlyList<string> Warnings { get; }

    public string Summary => IsValid
        ? (Warnings.Count == 0 ? "Configuration valide." : string.Join(" ", Warnings))
        : string.Join(" ", Errors);

    public static ValidationResult Ok(IReadOnlyList<string> warnings) =>
        new(true, Array.Empty<string>(), warnings);

    public static ValidationResult Fail(IReadOnlyList<string> errors, IReadOnlyList<string> warnings) =>
        new(false, errors, warnings);
}

public static class BroadcastPlanValidator
{
    public static ValidationResult Validate(BroadcastPlan plan)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (plan.Tv == null || string.IsNullOrWhiteSpace(plan.Tv.Id))
            errors.Add("EECC TV doit toujours être une destination.");
        else if (plan.Tv.Port is < 1 or > 65535)
            errors.Add("Le port EECC TV est invalide.");

        var externals = plan.Externals ?? Array.Empty<ExternalDestination>();
        var expected = plan.Mode switch
        {
            BroadcastMode.TvOnly => "aucune destination externe",
            BroadcastMode.TvPlusOne => "exactement une destination externe",
            BroadcastMode.TvPlusThreeOrMore => "au moins trois destinations externes",
            _ => "un mode connu"
        };

        var countOk = plan.Mode switch
        {
            BroadcastMode.TvOnly => externals.Count == 0,
            BroadcastMode.TvPlusOne => externals.Count == 1,
            BroadcastMode.TvPlusThreeOrMore => externals.Count >= 3,
            _ => false
        };

        if (!countOk)
        {
            errors.Add(
                $"Le mode « {BroadcastModeText.Label(plan.Mode)} » exige {expected}. " +
                $"{externals.Count} destination(s) externe(s) sont configurée(s). EECC TV reste obligatoire.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (plan.Tv != null && !string.IsNullOrWhiteSpace(plan.Tv.Id))
            ids.Add(plan.Tv.Id);

        foreach (var destination in externals)
        {
            if (string.IsNullOrWhiteSpace(destination.Id) || !ids.Add(destination.Id))
                errors.Add("Chaque destination doit avoir un identifiant unique, distinct d'EECC TV.");

            if (string.IsNullOrWhiteSpace(destination.DisplayName))
                errors.Add("Chaque destination externe doit avoir un nom.");

            if (destination.StreamKey.IsEmpty)
                errors.Add($"La clé de flux est absente pour « {SafeName(destination)} ».");

            if (!PublishUrl.TryParseIngest(destination.IngestUrl, out var uri, out var urlError) || uri == null)
            {
                errors.Add($"{SafeName(destination)} : {urlError}");
                continue;
            }

            var key = destination.StreamKey.Reveal();
            if (!destination.StreamKey.IsEmpty &&
                destination.IngestUrl.Contains(key, StringComparison.Ordinal))
            {
                errors.Add($"La clé de flux de « {SafeName(destination)} » ne doit pas être écrite dans l'adresse.");
            }

            if (!plan.AllowPublicIngest && !EndpointPolicy.IsLocalOrPrivate(uri))
            {
                errors.Add(
                    $"« {SafeName(destination)} » pointe hors du réseau local ({PublishUrl.Redacted(destination.IngestUrl)}). " +
                    "Confirmez l'envoi hors réseau pour continuer. YouTube, Facebook et TikTok ne sont pas contactés par les tests.");
            }

            var note = BroadcastModeText.PlatformWarning(destination.Platform);
            if (note != null)
                warnings.Add(note);
        }

        return errors.Count == 0
            ? ValidationResult.Ok(warnings)
            : ValidationResult.Fail(errors, warnings);
    }

    private static string SafeName(ExternalDestination destination) =>
        string.IsNullOrWhiteSpace(destination.DisplayName) ? "Destination externe" : destination.DisplayName;
}
