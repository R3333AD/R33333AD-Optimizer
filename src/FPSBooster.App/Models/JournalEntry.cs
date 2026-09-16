namespace FPSBooster.App.Models;

/// <summary>Une modification réversible (registre, service ou plan d'alimentation).</summary>
public record JournalEntry(
    string Time,
    string Area,      // "reg" | "service" | "power"
    string Hive,      // ex "LocalMachine" (reg) ou nom du service / "" (power)
    string Key,       // sous-clé registre (reg)
    string Name,      // nom de valeur (reg)
    string Kind,      // RegistryValueKind en string (reg)
    bool Existed,     // la valeur existait-elle avant ?
    string? OldValue, // encodée (texte, nombre, base64)
    string? ActionId = null);// tweak d'origine (null = ancien format / inconnu)
