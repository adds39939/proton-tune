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
