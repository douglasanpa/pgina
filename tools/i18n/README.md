# Translations

pGina's windows are written in English, and English text is the key for every
translation. Each C# project carries its catalog in `Languages/<lang>.po`
(gettext format), embedded in the assembly at build time.

## Updating the catalogs

After changing texts in a form or in code, run from the repository root:

    python tools/i18n/update-po.py

It collects the texts of every project (designer captions, tooltips, column
headers, `.resx` texts and the `Language.T` / `Language.Format` calls), keeps the
translations already in the catalogs, and lists what is still untranslated. Fill
the empty `msgstr` entries with any PO editor or a text editor, and run it again.
`--check` only reports, and fails when something is untranslated.

## In code

- Forms call `FormStyle.Apply(this)` after `InitializeComponent()`, which
  translates every caption, tooltip and column of the form.
- Texts built in code go through `Language.T("...")` or
  `Language.Format("... {0}", value)`. Log messages stay in English.

## Language selection

1. `PGINA_LANGUAGE` environment variable (for tests and screenshots);
2. the `Language` value under `HKLM\SOFTWARE\pGina3.fork`, set in
   Configuration > General;
3. the Windows display language.

Any Portuguese variant uses `pt-BR`; everything else uses English.

## Logon screen

The credential provider is C++ and keeps its texts in
`pGina/src/CredentialProvider/Localization.cpp`. Add new texts there as well.
