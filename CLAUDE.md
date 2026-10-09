# Lunar Escape VR — заметки для Claude

Unity 6000.6.1f1 (ровно эта версия), URP, XR Interaction Toolkit 3.6, OpenXR, цель — HTC VIVE (wand, тачпад).
Команда смешанная: README и Docs на китайском, общение с пользователем — по-русски. Код: комментарии на **китайском** (правило README), стиль как в соседних файлах.

## Главное
- **Основная сцена — `Assets/_LunarEscape/Scenes/11_LunarStation_LifeSupport.unity`** (жизнеобеспечение, скафандр, кислород, шлюз, полёт, стыковка). Сцены 04–10 — старые учебные этапы, их не ломать.
- Сцены **собираются Editor-скриптами** (`Assets/_LunarEscape/Editor/Build*.cs`, `Install*.cs`), а не руками. Новые объекты добавляй установщиком (идемпотентным: удалить старое → построить заново), а не правкой YAML сцены.
- Порядок меню **Lunar Escape** для сцены 11 после её пересборки:
  1. `Install Life Support and Cockpit` (скрипт друга, строит сцену 11)
  2. `Install Interaction Feedback (All Scenes)` — звуки и вибрация
  3. `Install Airlock Repair (Life Support Scene)` — ремонт шлюза
  4. `Install Station Start: Lights, Spawn, Movement (Life Support Scene)`
  5. `Install Voice Hints and Decompression (Life Support Scene)` — голос ЦУПа/командира и разгерметизация
- Логика миссии: `StationMission` (фазы и таймер) + `LifeSupportMission` (воздух, питание, температура, скафандр, `DoorRepaired`/`DoorOpen`). Открытие двери без скафандра = смерть.
- Шлюз (`Scripts/Airlock`): 3 поломки (предохранитель на левой стене + запасной на задней, вентиль справа от двери, 3 болта на двери ключом) + рычаг удержания → `AirlockRepair.IsReleased` → `LifeSupportMission.DoorRepaired`. Дверь открывает кнопка на экране шлюза друга.
- Физические органы управления — `Scripts/Cockpit/CockpitControl` (база на `XRBaseInteractable`), `CockpitSwitch`, `CockpitLamp`, `ReleaseLever`, `ValveWheel`.
- Обратная связь: `Scripts/Feedback` (`FeedbackSounds` синтезирует звуки кодом, `HandHaptics`).
- Голос (`Scripts/Voice`): `MissionVoice` — очередь реплик (ЦУП по «рации» с фильтрами, командир из своей позиции), `MissionVoiceDirector` — кто и когда говорит (опрос состояния миссии, напоминания). Реплики: `Tools/voice_lines.tsv` → `Tools/generate_voice_lines.ps1` (Windows TTS: Irina = ЦУП, Pavel = командир) → `Audio/Voice/*.wav`; после новых реплик перезапустить установщик 5. Длинных инструкций на экранах больше нет — их проговаривает ЦУП.
- Разгерметизация: `AirlockDecompression` — на фронте `DoorOpen` рёв, пыль/туман к двери, незакреплённые `Rigidbody` тянет к проёму, вибрация, игрока тянет ~0.4 м (`pullPlayer`).
- Темнота до рубильника: `LifeSupportEnvironment.ConfigureBlackout` гасит светящиеся материалы (`LB_LightDiffuser`, `LB_EmergencyGreen`), отражения скайбокса (главная причина «светло без ламп»), амбиент и солнце (днём просвечивает сквозь стены).
- Свет: `HabitatBreaker` (рубильник) → `LifeSupportEnvironment`. Бонусы времени: `MissionTimeBonus`.
- Расстановку на стенах реалистичной станции мерить лучом по видимой геометрии (см. `InstallAirlockRepair.MeasureWalls`) и проверять рендером — стены и мебель не там, где старые заготовки.

## Проверка без окна (Unity должен быть закрыт)
```
"C:/Programs/Unity/6000.6.1f1/Editor/Unity.exe" -batchmode -projectPath "C:/Projects/VR/VR_game" -executeMethod <Namespace.Class.Method> -quit -logFile <log>
"C:/Programs/Unity/6000.6.1f1/Editor/Unity.exe" -batchmode -projectPath "C:/Projects/VR/VR_game" -runTests -testPlatform PlayMode [-testFilter "LunarEscape.Tests.X|..."] -testResults <xml> -logFile <log>
```
Полный PlayMode-набор ~2 мин (≈157 тестов). Ошибки компиляции — `error CS` в логе. Для визуальной проверки — временный Editor-скрипт, рендерящий камерой в PNG (удалять после).
Тесты пишут скриншоты в `Docs/Previews` — их и шум Unity в `ProjectSettings/` **не коммитить**; `Assets/InitTestScene*.unity` удалять.
В worktree (`.claude/worktrees/...`) `-projectPath` указывать на сам worktree (своя `Library`, первый импорт ~10 мин); `AscentSceneTests.ThreeLanguagePanel…` там падает — пишет в `../../.development`, которой нет.

## Известное
- GPU Resident Drawer выключен (`PC_RPAsset`), иначе Unity 6.6 падает при входе в Play.
- Клавиатурный симулятор XRI в сцене обычно выключен; в тестах его отключают в `Load()`.
- Ключ на болтах в симуляторе крутится клавишей **F** (только в редакторе).
- `Lunar Escape → Skip Airlock Repair` / `Switch Station Lights On` — быстрые обходы в Play Mode.

## Git
- Репозиторий `Micheal668/VR_game`, ветка `main`. Работать в отдельной ветке, в `main` — только fast-forward, **никогда не force push** (однажды это стёрло работу).
- `our-interactions` — архив нашей старой версии (пульт кабины, пояс-гнёзда — не перенесены, по решению пользователя).
- Перед работой: `git fetch` и проверить, не менял ли друг `main`.
