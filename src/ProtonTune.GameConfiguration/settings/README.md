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
    restrictToProtonBuild: true     # optional: hide it elsewhere, rather than greying it out.
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

## Sections that configure a command

Not everything worth setting is an environment variable. Gamescope is configured entirely by flags
on the command that launches it, so a section may declare a `command` block alongside — or instead
of — its `settings`.

```yaml
command:
  name: gamescope           # The command written into the launch chain.
  label: Launch through Gamescope
  description: What running the game through it does.
  terminator: "--"          # optional: what ends the command's own arguments.
  groups:
    - name: Output          # optional: a group with no name shows no heading.
      flags:
        - flag: "-W"                    # As it is written, leading dashes and all.
          aliases: ["--output-width"]   # optional: other spellings, recognised when read.
          label: Output width
          kind: number                  # toggle | choice | text | number
          placeholder: "3840"
          description: One sentence on what it does.

        - flag: "-f"        # no kind, so a switch: written bare, with no value.
          label: Fullscreen
```

Only `name` and `flag` are required. As with a compound variable's options, a flag's `kind`
defaults to `toggle` rather than `text`, because command lines are mostly switches.

`terminator` is what tells ProtonTune where the command's arguments stop and the next command in
the chain begins. Declare it for any command that takes one; a command without it — `mangohud`,
`gamemoderun` — is treated as taking no arguments of its own rather than claiming what follows it.

Setting any flag adds the command to the chain if it is not already there, and switching the
command off takes its flags with it. Flags ProtonTune does not list survive both untouched, and
`aliases` is what stops a flag someone wrote out in full from being duplicated rather than edited.

## `protonBuilds`

A list of regular expressions matched against a build's name and its version string. If any
matches, the setting applies; if the list is absent, the setting is offered for every build.

Use it for a setting that only ever exists in one family of builds. It is a declaration, not a
guess — separately from this, ProtonTune reads each installed build's own launch script and dims
anything that build does not consult. The two agree in the usual case, and where a variable is
implemented somewhere ProtonTune cannot read, this list is the only thing that can speak for it.

By default a setting that does not apply is still shown, greyed out, saying why it does nothing.
Add `restrictToProtonBuild: true` to hide it instead. Use that where the setting exists in one
family of builds and nowhere else: a list of GE-Proton features shown against Valve's Proton is
not a set of choices to reconsider, it is noise in a list someone is trying to read. A setting
that already has a value stays visible whatever this says, or it could neither be seen nor
removed.

## Three ids the application knows by name

`dlss`, `cpu`, and `mangohud` each carry a control that is more than a text box — the library
swap, the affinity picker, and the option-by-option MangoHud editor. Renaming those ids removes
the control rather than the section, so rename them only alongside the code that looks for them.

## Variables ProtonTune does not know

Anything missing from these files still parses and is still written back. It appears under custom
variables rather than in a named section, so an unknown variable costs presentation and never
data.
