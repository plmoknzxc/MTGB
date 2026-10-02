#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;

public static class CardArtImportValidation
{
    public static string Run()
    {
        var card = new CardDefinition { setCode = "IMPORT_TEST_" + Guid.NewGuid().ToString("N"), collectorNumberText = "1" };
        card.features.Add(new CardFeatureDefinition { faceIndex = 1 });
        string first = Path.Combine(CardImageLibrary.ExternalRoot, "2ED", "162.jpg");
        string second = Path.Combine(CardImageLibrary.ExternalRoot, "10E", "376.jpg");
        string key = card.PrintingKey;
        try
        {
            string target = CardArtImportService.Import(card, first);
            Require(Convert.ToBase64String(File.ReadAllBytes(target)) == Convert.ToBase64String(File.ReadAllBytes(first)), "Source bytes changed");
            Require(CardImageLibrary.TryFindImagePath(card, 0, out var selected) && selected == target, "Custom priority");
            Require(!CardImageLibrary.TryFindImagePath(card, 0, out _, false), "Custom treated as downloaded official art");
            Require(CardImageLibrary.TryLoadFullCard(card, out var sprite) && sprite != null, "Custom image display");
            CardArtImportService.Import(card, second);
            Require(Convert.ToBase64String(File.ReadAllBytes(target)) == Convert.ToBase64String(File.ReadAllBytes(second)), "Replacement failed");
            bool rejected = false;
            try { CardArtImportService.Import(card, Path.Combine(PrintingKeyUtility.ApplicationRoot, "card-images.example.json")); }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected && Convert.ToBase64String(File.ReadAllBytes(target)) == Convert.ToBase64String(File.ReadAllBytes(second)), "Invalid import replaced good art");
            CardArtImportService.Import(card, first, 1);
            Require(CardImageLibrary.TryLoadBackFace(card, out sprite), "Back face import");
            Require(CardArtImportService.Remove(card) && !CardArtImportService.TryGetPath(card, 0, out _), "Remove override");
            Require(CardArtImportService.TryGetPath(card, 1, out _), "Front removal affected back");
            Require(card.PrintingKey == key, "Rules identity changed");
            bool badFace = false;
            try { CardArtImportService.Import(card, first, 2); } catch (ArgumentOutOfRangeException) { badFace = true; }
            Require(badFace, "Invalid face accepted");
            var bolt = CardDatabaseRepository.FindByPrintingKey("2ED", "162");
            // Test recovery on a synthetic identity whose fallback is an installed real printing.
            card.oracleID = bolt.oracleID;
            Require(CardImageLibrary.TryLoadFullCard(card, out sprite) && sprite != null, "Restore base art");
            return "PASS: import byte preservation, priority, official status isolation, display, replacement, invalid input preservation, back face, remove, restore, face validation";
        }
        finally
        {
            CardArtImportService.Remove(card, 0);
            CardArtImportService.Remove(card, 1);
        }
    }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}
#endif
