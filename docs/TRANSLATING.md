# Translating Customisable UI

You can add a language to Customisable UI without writing any code: it is two small text files and a text editor. Anything
that isn't translated is shown in English, so a partial translation is fine, and so is translating one more string later.

Everything the mod shows (setting names and explanations, group titles, colour names, presets and the hotkey message) comes
from a template, `Module/ModuleData/Languages/EN/strings.xml`. Each line has an `id` the mod uses to find it and the English
`text`. A translation is a copy of that file with the `text` replaced.

## Add a language

**1. Copy the English folder.** Copy `Module/ModuleData/Languages/EN` to a new folder next to it, named after the language
(`DE`, `FR`, `RU`...). Use the folder name the game itself uses, from the table below.

**2. Edit `language_data.xml`** in your new folder. Set `id`, `name`, `subtitle_extension` and `supported_iso` to the values
for your language, and point `xml_path` at your folder. For German:

```xml
<?xml version="1.0" encoding="utf-8"?>
<LanguageData xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
              xsi:noNamespaceSchemaLocation="https://raw.githubusercontent.com/BUTR/Bannerlord.XmlSchemas/master/ModuleLanguageData.xsd"
              id="Deutsch"
              name="Deutsch"
              subtitle_extension="de"
              supported_iso="de,deu,ger,de-de,de-at,de-li,de-lu,de-ch"
              under_development="false">
  <LanguageFile xml_path="DE/strings.xml" />
</LanguageData>
```

The `id` must match the game's own name for the language exactly, or the game won't connect your strings to it. These are the
languages the game ships with:

| Folder | `id` and `name` | `subtitle_extension` | `supported_iso` |
|--------|-----------------|----------------------|-----------------|
| `BR`   | `Português (BR)` | `pt` | `pt,por,pt-pt,pt-br` |
| `CNs`  | `简体中文` | `zh-HANS` | `zh-HANS,zh,zho,chi,zh-cn,zh-sg` |
| `CNt`  | `繁體中文` | `zh-HANT` | `zh-HANT,zh-tw,zh-hk` |
| `DE`   | `Deutsch` | `de` | `de,deu,ger,de-de,de-at,de-li,de-lu,de-ch` |
| `FR`   | `Français` | `fr` | `fr,fra,fr-fr,fr-be,fr-ca,fr-ch` |
| `IT`   | `Italiano` | `it` | `it,ita,it-it,it-ch` |
| `JP`   | `日本語` | `jp` | `ja,jpn,ja-ja,ja-jp,jp-jp` |
| `KO`   | `한국어` | `ko` | `ko-kr,ko,kor,kr-kr` |
| `PL`   | `Polski` | `pl` | `pl,pol,pl-pl` |
| `RU`   | `Русский` | `ru` | `ru,rus,ru-ru,ru-md` |
| `SP`   | `Español (LA)` | `es` | `es,spa,es-es,es-ve,es-uy,es-ar,es-bo,es-cl,es-co,es-cr,es-do,es-ec,es-sv,es-gt,es-hn,es-mx,es-ni,es-pa,es-py,es-pe,es-pr` |
| `TR`   | `Türkçe` | `tr-TR` | `tr,tur,tr-tr` |

The game's own copies are in `Modules/Native/ModuleData/Languages/<folder>/language_data.xml` in your Bannerlord folder.
For any other language, copy the same attributes from the game's file for it.

**3. Edit `strings.xml`** in your new folder:

- Change `<tag language="English" />` to your language's `id` (for German, `<tag language="Deutsch" />`).
- Delete the comment at the top.
- Translate the `text="..."` values. Leave every `id="..."` exactly as it is.
- Delete the lines you don't want to translate yet; they will show in English.

```xml
<string id="CUI_Enabled" text="Customisable UI aktivieren" />
<string id="CUI_Enabled_Hint" text="Hauptschalter. Aus stellt sofort das Original-Aussehen wieder her." />
```

Save both files as **UTF-8**.

## Rules

- **Never change an `id`.** A misspelt id is silently ignored by the game, which is why the check below exists.
- **No `{` or `}`** in a text: the game reads them as variables.
- Write `&` as `&amp;`, `<` as `&lt;` and `"` as `&quot;` (for example `text="Panels &amp; backgrounds"`).
- Leave `#RRGGBB`, key names such as `Ctrl+Shift+U`, and technical terms such as `WCAG` and `MCM` as they are.
- Group titles start with a number (`1. General`). Keep the numbers, so the groups read in the same order.
- Some explanations refer to a dropdown entry by name, such as `'Custom (use hex code)'`. Use the same wording you gave that
  entry (`CUI_Colour_Custom`) so the two match.
- Setting names sit in a narrow column. Short is better.

## Check your files

You don't need the game or the .NET SDK for this, just PowerShell (it is built into Windows). From the repository folder:

```powershell
./tools/check-translations.ps1 -Language DE
```

It confirms that the files are well-formed, that every id exists in the English template, that nothing is blank or
duplicated, and that no text contains `{` or `}`. It also reports how much of the template your language covers. The same
check runs on every pull request.

## Try it in the game

1. Install Customisable UI from a version that includes translation support (building from this repository does; see the
   README).
2. Copy your language folder into `<Bannerlord>/Modules/CustomisableUI/ModuleData/Languages/`.
3. Set the game to your language, then open Options, Mod Options, Customisable UI. If the text is still English, restart
   the game.

## Send it in

- **On GitHub:** fork the repository, add your folder under `Module/ModuleData/Languages/`, and open a pull request. Say how
  you would like to be credited.
- **Without GitHub:** zip your folder and attach it to a new issue, or send it through the mod's Nexus page.

## When the mod changes

New and reworded text is added to the English template with each release. Strings your language doesn't have yet are shown
in English until you add them. Run the check above after updating: it lists ids that no longer exist so you can remove
them.

## For developers

- Every string the player can see starts with a tag, `{=CUI_Something}English text`. The game shows the text after the tag
  unless the player's language has a translation for the id. This applies to MCM attributes (names, hints, groups),
  dropdown lists, preset names (`ModSettings.PresetName`), colour names (`Swatches.Tagged`) and messages (`Hotkey`). Ids
  are shared by every mod, so they all start with `CUI_`. Text must not contain `{` or `}`.
- After adding or changing text, regenerate the template and commit it:

  ```powershell
  dotnet run --project tests/CustomisableUI.Tests -c Release -- --write-strings
  ```

  The test suite fails if the template is out of date, or if any visible string is missing its tag.
- If the meaning of a string changes, give it a new id so older translations don't show the wrong thing.
