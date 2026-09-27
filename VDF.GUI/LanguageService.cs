// /*
//     Copyright (C) 2026 0x90d
//     This file is part of VideoDuplicateFinder
//     VideoDuplicateFinder is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//     VideoDuplicateFinder is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//     You should have received a copy of the GNU Affero General Public License
//     along with VideoDuplicateFinder.  If not, see <http://www.gnu.org/licenses/>.
// */
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Platform;
using ReactiveUI;

namespace VDF.GUI {
	public class LanguageService : ReactiveObject {
		Dictionary<string, string> _translations = new();

		public IReadOnlyList<string> AvailableLanguages => field ??= LoadAvailableLanguages();
		public string CurrentLanguage {
			get;
			set {
				value = NormalizeLanguageCode(value);
				if (EqualityComparer<string>.Default.Equals(field, value))
					return;
				this.RaiseAndSetIfChanged(ref field, value);
				LoadLanguage(value);
			}
		} = "en";

		/// <summary>
		/// Maps a culture code onto a locale file we actually ship. Windows reports the
		/// UI culture as a bare two-letter tag ("zh"), while the locale files carry a
		/// script subtag ("zh-Hans") - a Chinese system used to probe for "zh.json",
		/// miss it, and silently fall back to English. Simplified and Traditional both
		/// land on zh-Hans because that is the only Chinese locale in Assets/Locales.
		/// </summary>
		public static string NormalizeLanguageCode(string? langCode) {
			if (string.IsNullOrWhiteSpace(langCode))
				return "en";
			var code = langCode.Trim();
			if (code.Equals("zh", StringComparison.OrdinalIgnoreCase)
				|| code.StartsWith("zh-", StringComparison.OrdinalIgnoreCase))
				return "zh-Hans";
			return code;
		}

		public void LoadLanguage(string langCode) {
			langCode = NormalizeLanguageCode(langCode);
			try {
				var uri = new Uri($"avares://VDF.GUI/Assets/Locales/{langCode}.json");
				using var stream = AssetLoader.Open(uri);
				using var reader = new StreamReader(stream);
				var json = reader.ReadToEnd();
				_translations = JsonSerializer.Deserialize(json, Data.GuiJsonContext.Default.DictionaryStringString) ?? new();
				this.RaisePropertyChanged("Item[]");
			}
			catch (Exception) {
				if (langCode != "en")
					LoadLanguage("en");
				else
					_translations = new();
			}
		}

		static IReadOnlyList<string> LoadAvailableLanguages() {
			try {
				var localeUri = new Uri("avares://VDF.GUI/Assets/Locales/");
				var assets = AssetLoader.GetAssets(localeUri, null)
					.Select(asset => Path.GetFileNameWithoutExtension(asset.AbsolutePath))
					.Where(name => !string.IsNullOrWhiteSpace(name))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
					.ToList();

				return assets.Count > 0 ? assets : new List<string> { "en" };
			}
			catch (Exception) {
				return new List<string> { "en" };
			}
		}
		public string this[string key] => _translations.TryGetValue(key, out var val) ? val : key;
	}
}
