import json
from pathlib import Path

rows = '''expansion.control.title|卧室应急解锁 / 中控|QUARTERS / CENTRAL CONTROL|ЖИЛОЙ ОТСЕК / УПРАВЛЕНИЕ
expansion.control.wait|先恢复电源，再完成实验室模块配对|Restore power, then match the lab modules|Восстановите питание и сопоставьте модули
expansion.control.unlock|确认 · 解锁卧室|CONFIRM · UNLOCK QUARTERS|ПОДТВЕРДИТЬ · ОТКРЫТЬ ОТСЕК
life.suit.ready|密封完成 · 玩家已穿戴|SEALED · PLAYER SUITED|ГЕРМЕТИЧНО · СКАФАНДР НАДЕТ
life.suit.sync|队友获救后独立穿戴|RESCUED CREW SUITS UP SEPARATELY|СПАСЁННЫЙ ЭКИПАЖ ОДЕВАЕТСЯ САМ
life.base.vented|舱门打开 · 基地快速泄压|HATCH OPEN · RAPID DECOMPRESSION|ЛЮК ОТКРЫТ · РАЗГЕРМЕТИЗАЦИЯ
airlock.latch.label|锁销 · 扳手接触 1.2 秒或转动|LATCH · HOLD WRENCH 1.2 s OR TURN|ЗАМОК · КЛЮЧ НА 1,2 с ИЛИ ПОВЕРНИТЕ
expansion.font.1|警报正在苏醒离开卧室检查备用电源卧室门故障先接通三色电路|ALARM WAKING UP LEAVE QUARTERS CHECK BACKUP POWER CREW TRAPPED CONNECT THREE CIRCUITS|ТРЕВОГА ПРОБУЖДЕНИЕ ВЫЙДИТЕ И ПРОВЕРЬТЕ ПИТАНИЕ ЭКИПАЖ ЗАПЕРТ СОЕДИНИТЕ ЦЕПИ
expansion.font.2|去实验室配对三个数据模块配对成功在中控按下解锁队友获救正在前往衣架穿服队友已穿服修好气闸后撤离|MATCH THREE LAB DATA MODULES MODULES READY CONFIRM AT CONTROL CREW RESCUED SUITING UP CREW SUITED REPAIR AIRLOCK AND EVACUATE|СОПОСТАВЬТЕ ТРИ МОДУЛЯ МОДУЛИ ГОТОВЫ ПОДТВЕРДИТЕ ЭКИПАЖ СПАСЕН НАДЕВАЕТ СКАФАНДР СКАФАНДР ГОТОВ ЭВАКУАЦИЯ
expansion.font.3|先恢复备用电源拉下总闸前往中控解锁按颜色与编号接线按形状与编号插入模块接错基地电量应急照明共用制氧机修复耗氧减半扳手保持秒进度|RESTORE BACKUP POWER PULL BREAKER CONFIRM AT CONTROL MATCH COLOR NUMBER MATCH SHAPE ERROR BASE POWER SHARED EMERGENCY OXYGEN GENERATOR REPAIRED HALF DRAIN HOLD WRENCH PROGRESS|ВОССТАНОВИТЕ ПИТАНИЕ ВКЛЮЧИТЕ РУБИЛЬНИК ПОДТВЕРДИТЕ НА ПУЛЬТЕ СОЕДИНИТЕ ЦВЕТ И НОМЕР СОПОСТАВЬТЕ ФОРМУ ОШИБКА ЗАРЯД БАЗЫ ОБЩЕЕ АВАРИЙНОЕ ОСВЕЩЕНИЕ ГЕНЕРАТОР КИСЛОРОДА ИСПРАВЕН РАСХОД ВДВОЕ МЕНЬШЕ УДЕРЖИВАЙТЕ КЛЮЧ
expansion.font.symbols|CCW CW ° – O2 01 CREW QUARTERS 02 RESEARCH LAB 03 LIFE SUPPORT SOLAR ARRAY BACKUP POWER METEORITE MOON SOIL DATA|CCW CW ° – O2|CCW CW ° – O2'''
path=Path('Assets/_LunarEscape/Localization/repair-texts.json')
data=json.loads(path.read_text(encoding='utf-8-sig')); existing={e['key']:e for e in data['entries']}
for row in rows.splitlines():
    key,zh,en,ru=row.split('|'); entry={'key':key,'zh':zh,'en':en,'ru':ru}
    if key in existing:existing[key].update(entry)
    else:data['entries'].append(entry)
path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
