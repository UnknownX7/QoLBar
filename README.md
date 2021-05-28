# QoL Bar
A [XIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher) plugin for creating custom menus for commands, similar to hotbars and macros.

## Getting Started
Upon first installing through `/xlplugins`, you will be greeted by a blank bar at the bottom of your screen, and the Demo Bar, which will showcase the various features that can be utilized. This bar will be updated over time to showcase any new features and may be freely deleted at any point, as it can be recreated by right clicking the `Import` button on the plugin config.

- Terminology
  - Bar: These are the custom menus that contain shortcuts, and are similar to hotbars
  - Shortcut: These are the buttons on a bar, and are similar to macros
  - Category: This is a shortcut that creates a popup containing more shortcuts
  - Spacer: This is a shortcut that has no function and exists specifically for information or to align other shortcuts
  - Pie: A bar brought up as a radial menu by holding a hotkey
- Navigation
  - Left click: Executes shortcuts or moves unlocked bars
  - Right click: Opens the configuration for the hovered shortcut or bar (the surrounding background)
  - Shift + Right click: Adds a new shortcut to the hovered bar or category
  - Double or Control + Left Click: Can be used on various options such as sliders to input custom amounts

You can edit the shortcuts on the Demo Bar in order to see how they function.

## Advanced Features
All of the following information is available through tooltips on the corresponding buttons or configuration inputs or in an example on the Demo Bar, but will be explained here in more detail.

### Tooltips
To add a tooltip to a shortcut, put `##` at the end of its name to use the following text as a tooltip, e.g. `Plugins##Opens the plugin installer`.

### Icons
To use an icon from the game, use `::#` as the shortcut's name, where # is the icon's ID, e.g. `::1`. These can also utilize tooltips in the same manner as above.

### Icon Arguments
Icons can have modifiers (`::x#`) which will change the way they display, these are detailed in the `Name` input tooltip when editing a shortcut with an icon. The most common use case is adding the glossy frame that the hotbar applies to all icons, i.e. `::f2914`.

### Icon Browser
This plugin comes with a built-in browser for finding various icons, it can be accessed through the magnifying glass in the corner when editing / adding shortcuts, or through `/qolicons`. Clicking on these icons will copy their ID in the format `::#`, so that you can paste them into the name of shortcuts. Additionally, opening a shortcut's configuration while this menu is open will automatically change it to use the last clicked icon.

### Custom Icons
To use a custom icon, follow the instructions on the Icon Browser's `Custom` tab.

### Shortcut Mode
The mode of a shortcut can be changed so that the command is executed differently. `Incremental` will change it so that the shortcut executes the first line on the first press, the second line on the second press, and so on. `Random` will cause the shortcut to execute a random line each time instead. Categories may also have their mode changed and will no longer open a popup upon being pressed. Instead they will act like a shortcut using a different mode, except using the shortcuts contained within instead of lines.

### Bar Dock / Alignment
The dock and alignment of a bar will determine the side of the game window that the bar will position itself relative to. If you want to place a bar outside of the game, you can do so by using the `Undocked` option. This will let you drag the bar out of the game window (assuming the Dalamud option is enabled) and will change the position to be relative to the top left corner of your monitor.

### Pies
By adding a hotkey to a bar, you can bring it up as a pie. Note that a bar can be hidden through `/qolvisible` and its hotkey will still work.

### Condition Sets
By adding a condition set to a bar through the plugin config's `Bar Manager` tab, it will automatically hide or reveal depending on the current game state. These can be created through the `Condition Sets` tab.

### Running Macros
To run a macro from a shortcut, use `//m#` in the command, where # is the slot of the macro, e.g. `//m99`. In order to use the shared tab of macros you need to add 100 to the slot, e.g. `//m100`.

### Custom Macros
`//m` also supports not specifying a macro slot in order to run the following commands through its own macro system.

E.g.
```
//m
/macrolock
/wait 1
/echo Macro finished
//m
```
will function identically to having a macro with those commands in individual slot #0 and just using `//m0`.

### Comments
Shortcuts support comments if you would like to add notes inside of the command, i.e. `// This is a comment`.

### Exporting / Importing
You can share a bar or shortcut by pressing the `Export` button when editing it, this will copy a block of text that you can send to another person. In order to use this block of text, simply copy it and press the `Import` button on the plugin config's `Bar Manager` tab to import the text as a bar. You may also use the `Import` button when adding a new shortcut to import all shortcuts inside of the text into a preexisting bar or category.

## FAQ
> I'm completely lost and don't understand how to make my own bars or shortcuts

Be sure to play around with the Demo Bar for a bit before trying to create your own bar. Additionally, create the bar without trying to utilize advanced features, i.e. just edit the `Name` and `Command` of shortcuts. After the bar contains all of the basic functions you want it to have, then you can start changing the way it looks, or adding advanced features. If you absolutely cannot solve how to do something, feel free to ask in the Dalamud Discord for assistance.


> /wait doesn't appear to work!

Shortcuts do not have the ability to wait or use any other macro specific command. You will need to run a macro (or use the custom macro feature) with the shortcut instead.


> Where can I find bars / shortcuts that other people have made?

The Dalamud Discord has a plugin preset channel that contains various presets that people have created.


> Where is my config located?

`%AppData%\XIVLauncher\pluginConfigs\QoLBar.json`


> How can I suggest a feature?

Ping me on the Dalamud Discord or [create an issue](https://github.com/UnknownX7/QoLBar/issues/new).


> X isn't working!

See above.
