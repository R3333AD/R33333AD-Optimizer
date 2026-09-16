using System.ComponentModel;

namespace FPSBooster.App.Services;

/// <summary>Logique de tri des colonnes (testable sans fenêtre).</summary>
public static class UiSortHelper
{
    public static string ResolveSortColumn(string? bindingPath) => bindingPath switch
    {
        "DisplaySize" => "SizeKb",
        string p when !string.IsNullOrEmpty(p) => p,
        _ => "DisplayName",
    };

    public static ListSortDirection NextDirection(string col, string currentCol, ListSortDirection current) =>
        col == currentCol
            ? (current == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending)
            : (col == "SizeKb" ? ListSortDirection.Descending : ListSortDirection.Ascending);
}
