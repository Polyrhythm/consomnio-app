
using System;

namespace Miniscript {
	public static class Keywords {
		public static string[] all = {
			"break",
			"continue",
			"else",
			"end",
			"for",
			"function",
			"if",
			"in",
			"new",
			"null",
			"then",
			"repeat",
			"return",
			"while",
			"and",
			"or",
			"not"
		};

		public static bool IsKeyword(string text) {
			return Array.IndexOf(all, text) >= 0;
		}

	}
}

