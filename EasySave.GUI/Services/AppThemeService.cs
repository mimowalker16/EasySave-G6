using System;
using System.Collections.Generic;
using System.Windows.Media;
using WpfApplication = System.Windows.Application;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;

namespace EasySave.GUI.Services
{
    public static class AppThemeService
    {
        public static readonly string[] PaletteNames =
        {
            "Midnight",
            "Forest",
            "Ocean",
            "Burgundy",
            "HighContrast"
        };

        private static readonly Dictionary<string, Palette> Palettes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Midnight"] = new(
                BgDeep: "#0F1117",
                BgCard: "#1A1D2E",
                BgHover: "#23263A",
                Accent: "#5865F2",
                AccentHover: "#4752C4",
                Success: "#3BA55D",
                Danger: "#ED4245",
                Warning: "#FAA61A",
                TextPrimary: "#FFFFFF",
                TextSecondary: "#99AAB5",
                Border: "#2C2F45",
                InputBackground: "#111522",
                MutedSurface: "#151927",
                ChipIdle: "#3B4255",
                ChipRunning: "#2F7D52",
                ChipPaused: "#A67318",
                ChipError: "#A93B3E"),

            ["Forest"] = new(
                BgDeep: "#101412",
                BgCard: "#18211D",
                BgHover: "#223128",
                Accent: "#4F9F72",
                AccentHover: "#3C7E59",
                Success: "#69B77D",
                Danger: "#D95F5F",
                Warning: "#DDAA3C",
                TextPrimary: "#F5FBF7",
                TextSecondary: "#A8BDB1",
                Border: "#2C3B32",
                InputBackground: "#121A16",
                MutedSurface: "#16251D",
                ChipIdle: "#3C4C43",
                ChipRunning: "#337A53",
                ChipPaused: "#986F22",
                ChipError: "#913A3F"),

            ["Ocean"] = new(
                BgDeep: "#0D1418",
                BgCard: "#15242B",
                BgHover: "#1E3440",
                Accent: "#2F9FCF",
                AccentHover: "#247EA5",
                Success: "#52B788",
                Danger: "#E05A5A",
                Warning: "#E2A944",
                TextPrimary: "#F3FAFC",
                TextSecondary: "#9EB6C1",
                Border: "#29404A",
                InputBackground: "#101C22",
                MutedSurface: "#132A33",
                ChipIdle: "#344B55",
                ChipRunning: "#2F7D66",
                ChipPaused: "#936E2A",
                ChipError: "#9A3D45"),

            ["Burgundy"] = new(
                BgDeep: "#171113",
                BgCard: "#26191E",
                BgHover: "#35242A",
                Accent: "#C15B7B",
                AccentHover: "#9C4761",
                Success: "#69A66A",
                Danger: "#E46464",
                Warning: "#D79B3E",
                TextPrimary: "#FFF7F9",
                TextSecondary: "#C2A8B0",
                Border: "#433039",
                InputBackground: "#1D1418",
                MutedSurface: "#22171C",
                ChipIdle: "#57434A",
                ChipRunning: "#4F8757",
                ChipPaused: "#946B26",
                ChipError: "#9B3F4A"),

            ["HighContrast"] = new(
                BgDeep: "#050608",
                BgCard: "#111318",
                BgHover: "#1B1F27",
                Accent: "#00B7FF",
                AccentHover: "#008FD0",
                Success: "#3DDB86",
                Danger: "#FF5C5C",
                Warning: "#FFC247",
                TextPrimary: "#FFFFFF",
                TextSecondary: "#D6DEE8",
                Border: "#596170",
                InputBackground: "#090B10",
                MutedSurface: "#141821",
                ChipIdle: "#596170",
                ChipRunning: "#17804D",
                ChipPaused: "#A77700",
                ChipError: "#B3313D")
        };

        public static int GetPaletteIndex(string? paletteName)
        {
            for (int i = 0; i < PaletteNames.Length; i++)
            {
                if (PaletteNames[i].Equals(paletteName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return 0;
        }

        public static string GetPaletteName(int index)
            => index >= 0 && index < PaletteNames.Length ? PaletteNames[index] : PaletteNames[0];

        public static void ApplyPalette(string? paletteName)
        {
            string selected = Palettes.ContainsKey(paletteName ?? string.Empty)
                ? paletteName!
                : PaletteNames[0];

            Palette palette = Palettes[selected];
            SetBrush("BgDeep", palette.BgDeep);
            SetBrush("BgCard", palette.BgCard);
            SetBrush("BgHover", palette.BgHover);
            SetBrush("Accent", palette.Accent);
            SetBrush("AccentHover", palette.AccentHover);
            SetBrush("Success", palette.Success);
            SetBrush("Danger", palette.Danger);
            SetBrush("Warning", palette.Warning);
            SetBrush("TextPrimary", palette.TextPrimary);
            SetBrush("TextSecondary", palette.TextSecondary);
            SetBrush("BorderBrush", palette.Border);
            SetBrush("InputBackground", palette.InputBackground);
            SetBrush("MutedSurface", palette.MutedSurface);
            SetBrush("ChipIdle", palette.ChipIdle);
            SetBrush("ChipRunning", palette.ChipRunning);
            SetBrush("ChipPaused", palette.ChipPaused);
            SetBrush("ChipError", palette.ChipError);
        }

        private static void SetBrush(string key, string color)
        {
            WpfApplication.Current.Resources[key] = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(color));
        }

        private sealed record Palette(
            string BgDeep,
            string BgCard,
            string BgHover,
            string Accent,
            string AccentHover,
            string Success,
            string Danger,
            string Warning,
            string TextPrimary,
            string TextSecondary,
            string Border,
            string InputBackground,
            string MutedSurface,
            string ChipIdle,
            string ChipRunning,
            string ChipPaused,
            string ChipError);
    }
}
