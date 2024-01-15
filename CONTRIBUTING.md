# Contributing to StableSwarmUI

Please open an Issue or Discussion, or ask on Discord before opening a pull request, to make sure your work doesn't overlap with others.

(TODO: More general contributing info)

## Languages

Want to help translate Swarm into another language?

- First: you're going to have to speak English. The English text is the "one true root" language that all other languages are derived from, it would be problematic to translate a translation.
- Are you just helping improve an existing language?
    - Great! Just edit the file in `languages/(language-code).json` and improve the translations included
- Do you want to add a new language?
    - See example here: (TODO: Link)
    - In short: copy/paste `languages/en.json` to `languages/(your-code).json`, fill out the info at the top, and start translating keys.
    - You can use https://github.com/mcmonkeyprojects/translate-tool to fill out any keys you can't be bothered filling in yourself with automatic AI-powered translation
- Are you adding new translatable keys?
    - I use the hidden webconsole call `debugSubmitTranslatables()` to generate `languages/en.debug` which contains a raw key list, and then use `--add-json` to add it in with the translate tool.

# Legal

By submitting a contribution to this repo, you agree to grant a perpetual, worldwide, non-exclusive, royalty-free, irrevocable license to the Stability.AI organization to use, copy, modify, and distribute your contribution under the terms of the MIT License, view [LICENSE.txt](/LICENSE.txt) for details, and under any future license we may change to.
