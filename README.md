# ScheduleOne-L10n - Tiny Localization for Schedule I Mods

> 🛟 **Need help or found a bug?** Get support at [support.doodesch.de/l10n](https://support.doodesch.de/l10n).

> One C# file that gives your MelonLoader mod proper translations - built-in language
> tables, player-editable JSON translation files, and automatic language detection.
> No DLL, no dependency, works on both the IL2CPP and Mono backends.

Schedule I itself has no language setting (it ships no localization system at all),
so this fills the gap for mods: English by default, the OS language when a translation
exists, and a single shared setting to force a language across every mod that uses it.

## For players: translate a mod 🌍

You can translate any mod that uses ScheduleOne-L10n - or fix a translation you don't
like - without touching the mod itself:

1. Start the game once with the mod installed.
2. Open `UserData/DooDesch/Localization/<ModName>/` in your game folder. You'll find
   `_template.en.json` with every text the mod can localize.
3. Copy it, rename the copy to your language code (`fr.json`, `es.json`, `pl.json`, ...)
   and translate the **values** - the keys are the mod's original English lines and must
   stay unchanged. Keep placeholders like `{0}` in your translation.
4. Restart the game. Done.

Your file wins over the mod's built-in translations, a few lines are enough (everything
else keeps its built-in text), and a file alone adds a whole new language. To force a
language regardless of your OS, set `Language` under `[DooDesch]` in
`UserData/MelonPreferences.cfg` (`auto`, `en`, `de`, `fr`, ...).

Made a full translation? You can publish it as its own **translation mod** on
Thunderstore or Nexus - a code-less package shipping
`Mods/Localization/<ModName>/<code>.json`. Everyone who installs it gets your
translation automatically; their own files still win over yours if they edit lines.

The full step-by-step guides live in the [wiki](https://docs.doodesch.de/mods/l10n/).

## For modders: adopt it in three steps 🔧

ScheduleOne-L10n is a single-file library - copy [`L10n.cs`](L10n.cs) into your project
(it compiles as `internal`, so it never collides with other mods doing the same).

1. **Wrap your player-facing strings.** The English literal *is* the key:

   ```csharp
   using DooDesch.Localization;

   entry.Title = L10n.T("Ask the motel manager about the RV");
   choice.Text = L10n.T("Pay ${0}", fee);   // string.Format placeholders work
   ```

2. **Ship translations** (optional - without any table your mod just stays English):

   ```csharp
   // in OnInitializeMelon
   L10n.Register("de", new Dictionary<string, string>
   {
       ["Ask the motel manager about the RV"] = "Frag die Motel-Managerin nach dem Wohnmobil",
       ["Pay ${0}"] = "{0} $ zahlen",
   });
   ```

3. **That's it.** Missing keys and unknown languages fall back to the English literal,
   players get `_template.en.json` exported automatically and can add their own
   languages, and the `[DooDesch] Language` preference is shared with every other
   L10n mod so players set it once.

Details, design notes and the adoption checklist are in the
[wiki](https://docs.doodesch.de/mods/l10n/).

## How the language is picked

1. `Language` under `[DooDesch]` in `MelonPreferences.cfg`, unless it is `auto`.
2. Otherwise the OS language (`Application.systemLanguage`), mapped to a two-letter
   code - `de`, `fr`, `es`, `it`, `pt`, `pl`, `ru`, `tr`, `ja`, `ko`, `zh`.
3. Everything else: `en`.

The language is resolved once per session; changing it needs a game restart.

For the resolved language, translations merge per entry from three sources, later wins:
built-in tables < installed translation mods (`Mods/Localization/...`) < the player's
own file (`UserData/DooDesch/Localization/...`).

## Mods using it

- [RVRepairVan](https://github.com/DooDesch-Mods/RVRepairVan) (English + German)

Using it in your own mod? Open an issue or ping me and I'll add you to the list.

## License

[MIT](LICENSE.md) - use it, ship it, modify it. A link back is appreciated but not required.
