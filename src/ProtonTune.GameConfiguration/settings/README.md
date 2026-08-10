# Setting definitions

One file per section of the configuration screen. Adding a section means adding a file; adding a
variable means adding an entry to one. Neither needs a change to the application.

```yaml
id: dlss                # Stable key. Referred to by name in code, so do not rename lightly.
title: DLSS             # The heading shown to a person.
order: 1                # Where the section sits in the list, lowest first.

settings:
  - variable: PROTON_DLSS_UPGRADE   # The environment variable, exactly as it is written.
    label: Upgrade DLSS libraries   # A readable name.
    description: What it actually does, in one sentence.
    kind: toggle                    # toggle | choice | text | number
    on: "1"                         # toggle only: the value written when it is switched on.
    choices: [a, b]                 # choice only: the values offered.
    placeholder: "144"              # text and number only: an example value.
    protonBuilds: ["^GE-Proton"]    # optional: builds this applies to, as regular expressions.
```

Only `variable` and `label` are required. `kind` defaults to `text`, and `on` to `1`.

## Variables that hold several settings at once

Some variables are really lists — `MANGOHUD_CONFIG`, `DXVK_HUD`. Give one a `compound` block and
it is edited option by option instead of as a line of text, whatever its `kind` says.

```yaml
  - variable: MANGOHUD_CONFIG
    label: MangoHud options
    compound:
      separator: ","          # between entries. Defaults to a comma.
      assignment: "="         # between a key and its value. Defaults to an equals sign.
      groups:
        - name: Frame limiting    # optional: a group with no name shows no heading.
          options:
            - key: fps_limit
              label: Frame rate limit
              kind: text          # toggle | choice | text | number
              placeholder: "224"
              description: One limit, or several separated by commas to cycle between them.

            - key: fps            # no kind, so a flag: written as the bare key, with no value.
              label: Frame rate
```

Two differences from an ordinary setting are worth knowing. An option's `kind` defaults to
`toggle`, not `text`, because these formats are mostly flags. And a toggle here writes the bare
key rather than a value, which is what a flag means.

These lists are always partial — MangoHud alone has well over a hundred options. Whatever is not
listed stays editable in the free-text field beneath the controls and is carried through
untouched, so nothing is out of reach and nothing is lost by ProtonTune not knowing about it.

## `protonBuilds`

A list of regular expressions matched against a build's name and its version string. If any
matches, the setting applies; if the list is absent, the setting is offered for every build.

Use it for a setting that only ever exists in one family of builds. It is a declaration, not a
guess — separately from this, ProtonTune reads each installed build's own launch script and dims
anything that build does not consult. The two agree in the usual case, and where a variable is
implemented somewhere ProtonTune cannot read, this list is the only thing that can speak for it.

## Three ids the application knows by name

`dlss`, `cpu`, and `mangohud` each carry a control that is more than a text box — the library
swap, the affinity picker, and the option-by-option MangoHud editor. Renaming those ids removes
the control rather than the section, so rename them only alongside the code that looks for them.

## Variables ProtonTune does not know

Anything missing from these files still parses and is still written back. It appears under custom
variables rather than in a named section, so an unknown variable costs presentation and never
data.
