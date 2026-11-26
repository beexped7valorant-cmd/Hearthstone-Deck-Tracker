# Hearthstone-Deck-Tracker

Hearthstone Deck Tracker is an automatic deck tracker and deck manager for Hearthstone.

## Сборка форка для Battlegrounds на Windows x64

Откройте успешный [запуск Main для ветки bg-tier7-recap](https://github.com/beexped7valorant-cmd/Hearthstone-Deck-Tracker/actions?query=workflow%3AMain+branch%3Abg-tier7-recap). В разделе **Artifacts** скачайте **HDT-Battlegrounds-Windows-x64**, распакуйте ZIP целиком, закройте старый HDT через трей и запустите **HearthstoneDeckTracker.exe**. Для скачивания артефакта GitHub нужен вход в GitHub. В самом трекере войдите в аккаунт HSReplay с активной подпиской Tier7.

- **Выбор героя.** Включите статистику выбора героя Tier7. Под портретами указаны доли финишей в верхней половине лобби и на первом месте. Зелёным выделен лидер по топ-4 в соло или топ-2 в дуэтах, золотым — по первому месту.
- **Ап таверны.** Подсказка появляется у золота во время найма. Базовые окна для Т2–Т6 приходятся на ходы 2, 5, 6–7, 8–9 и 10–12. При известной цене апа виден остаток золота или сколько не хватает. В настройках оверлея полей сражений у панели свой переключатель.
- **Наведение на витрину.** В соло наведите курсор на существо. Для текущего хода и версии карты HDT запрашивает статистику HSReplay по верхней половине MMR на текущем патче. Положительный Play Impact означает, что игроки с этой картой в среднем финишировали выше. Для побед в боях учитываются бои с победителем. Каждый показатель требует минимум 30 наблюдений в соответствующей выборке. Отдельная строка показывает связь карты с гайдами героя и состава. Для дуэтов и заклинаний показана доступность данных. Если сайт отклоняет запрос из HDT, откройте в настройках оверлея **Загрузить сохранённую статистику HSReplay**. Там можно открыть источник в браузере с вашей подпиской и загрузить его JSON на час. В подсказке будет дата этой выборки.
- **Тёмный дар.** До нажатия подсказка учитывает здоровье с бронёй, золото, доступность дара, цену апа и присутствие кор-карты выбранного направления в магазине. С седьмого хода учитывается преобладающий тип на вашем столе и тип закреплённого состава. После нажатия появляются три предложения с именами существ и их даров, когда игра передала все варианты. Совпадения с составом или типом героя отмечены рядом.
- **Кор-карты.** Откройте гайды по составам и раскройте **Основные карты**. Список содержит карты из пула матча с картинками, уровнем таверны и названиями составов. Заголовок-переключатель меняет группировку по тавернам на тир состава и таверну. Внутри гайда можно закрепить состав до конца матча. Без закрепления HDT предлагает направление, когда минимум две разные кор-карты на столе или в руке указывают на один лидирующий состав.
- **После боя.** Сводка считает среднее место, финиши в топе и первые места по рейтинговым играм текущей сессии в выбранном режиме.

## Installation
- Requirements:
  - Windows Vista or higher
  - .NET Framework 4.5 or higher
- [Download and run the installer](https://hsdecktracker.net/download/).

## Community & Troubleshooting
- [Follow HDT on X](https://x.com/hsdecktracker)
- Join the Community Discord: [![Join HearthSim Community Discord](https://discordapp.com/api/guilds/265636998700728321/widget.png)](https://hsreplay.net/discord)
- You can find the [FAQ here](https://github.com/HearthSim/Hearthstone-Deck-Tracker/wiki/FAQ).
- Streamers: Please read [Streaming instructions for OBS and XSplit](https://github.com/HearthSim/Hearthstone-Deck-Tracker/wiki/Streaming-Instructions) and [Twitch extension setup](https://hsdecktracker.net/twitch/setup/).
- HSReplay.net integration: Please email [support@hsreplay.net](mailto:support@hsreplay.net) for support.

## Contributing
Please read the [coding style and Commit/Pull Request guidelines](https://github.com/HearthSim/Hearthstone-Deck-Tracker/blob/master/CONTRIBUTING.md).

## Features
An in-game overlay:

![Overlay](https://github.com/HearthSim/Hearthstone-Deck-Tracker/raw/master/raw-assets/readme/overlay.png "Overlay")

The app:

![Tracker](https://github.com/HearthSim/Hearthstone-Deck-Tracker/raw/master/raw-assets/readme/hdt-ui.png "HDT UI")

- **Tracks**:
  - Cards left in your deck or cards drawn from your deck.
  - Your hand count, deck count and draw chances.
  - Cards played by your opponent.
  - Your opponent's hand count, deck count and probabilities of him having/drawing cards.
  - How long your opponent had each card in his hand and what cards have been mulliganed, stolen or returned.
- **Timer** for the current turn and total time spent for you and your opponent.
- The tracker tries to **automatically select the deck you are playing**.
- The cards and timer can either be displayed in an overlay (see screenshot) or in **extra windows** (Options > General > Additional Windows)
- **Deck Manager**:
  - **Import** decks from websites: arenavalue, hearthstats, hearthpwn, hearthhead, hearthstoneplayers, tempostorm, hearthstonetopdeck and hearthnews
  - Circumvent the 27 deck limit: Saved decks can be **exported to Hearthstone**. (My Decks > More...)
  - Decks can be filtered by custom **tags** and sorted by name, date and tags.
  - Set **notes** for each deck (My Decks > More...)
  - Create **screenshots** of decks (My Decks > More...)
  - **Share** your decks by exporting them as xml files or id-strings (My Decks > More...). Both can be imported via New Deck > Import.
- **Notifications**: get notified when a game or a turn starts (either by the tray icon flashing or hearthstone popping up)
- **Customization**: Almost every feature can be turned on/off separately.
- **Extensible via plugins**: [available plugins](https://github.com/HearthSim/Hearthstone-Deck-Tracker/wiki/Available-Plugins)
- **Stats per deck**:
  - Track the result of each game (win/loss), opponents, game mode and more
  - Win/loss rate vs each class.
  - Details for each game (cards drawn, played, etc.).
  - Select which game modes to track (Options > Other).
  - Import your opponent's (partial) deck from a tracked game as a new deck.

![Stats](https://i.imgur.com/Wke3Cuw.png "Deck stats")


## License

Copyright © [HearthSim](https://hearthsim.net). All Rights Reserved.
