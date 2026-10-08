"""Install the three-language strings used by scene 11 without replacing existing text."""
import json
from pathlib import Path

rows = r'''life.remaining|预计剩余|TIME LEFT|ОСТАЛОСЬ
life.oxygen|氧气储量|OXYGEN RESERVE|ЗАПАС КИСЛОРОДА
life.power|电池电量|BATTERY CHARGE|ЗАРЯД БАТАРЕИ
life.day|月昼|DAY|ДЕНЬ
life.night|月夜|NIGHT|НОЧЬ
life.base.title|环境控制 / 生命保障|ENVIRONMENT / LIFE SUPPORT|СРЕДА / ЖИЗНЕОБЕСПЕЧЕНИЕ
life.base.serial|月面基地 · ECLSS 01 · 压力舱|LUNAR HABITAT · ECLSS 01 · PRESSURIZED|ЛУННАЯ БАЗА · ECLSS 01 · ГЕРМООТСЕК
life.base.temperature|舱温 {0}°C  /  {1} · 月面 {2}°C|CABIN {0}°C  /  {1} · SURFACE {2}°C|В ОТСЕКЕ {0}°C  /  {1} · ПОВЕРХНОСТЬ {2}°C
life.base.sealed|舱门密封 · 氧气持续消耗|HATCH SEALED · OXYGEN IN USE|ЛЮК ЗАКРЫТ · КИСЛОРОД РАСХОДУЕТСЯ
life.base.vented|已泄压 · 舱内氧气 0%|VENTED · CABIN OXYGEN 0%|РАЗГЕРМЕТИЗАЦИЯ · КИСЛОРОД 0%
life.base.no_air|氧气耗尽 · 立即穿戴宇航服|NO OXYGEN · PUT ON YOUR SUIT|КИСЛОРОДА НЕТ · НАДЕНЬТЕ СКАФАНДР
life.base.no_power|基地断电 · 供氧消耗加速|POWER LOST · OXYGEN USE INCREASED|НЕТ ПИТАНИЯ · РАСХОД КИСЛОРОДА РАСТЁТ
life.cargo.title|应急物资 / 携带清单|EMERGENCY SUPPLIES / CARGO|АВАРИЙНЫЕ ЗАПАСЫ / ГРУЗ
life.suit.title|应急宇航服|EMERGENCY SUIT|АВАРИЙНЫЙ СКАФАНДР
life.suit.serial|EVA 01 · 密封检查|EVA 01 · SEAL CHECK|EVA 01 · ПРОВЕРКА ГЕРМЕТИЧНОСТИ
life.suit.progress|穿戴进度 {0}%|SUITING UP {0}%|НАДЕВАНИЕ {0}%
life.suit.ready|密封完成 · 乘员已同步穿戴|SEALED · BOTH CREW SUITED|ГЕРМЕТИЧНО · ОБА В СКАФАНДРАХ
life.suit.instructions|靠近服装架，持续握住手柄或按住下方按钮 {0} 秒。松开即暂停。|Stay at the rack. Hold the grip or button for {0}s. Release to pause.|У стойки удерживайте рукоятку или кнопку {0} с. Отпустите для паузы.
life.suit.get_supplies|先收纳氧气瓶和电池。开门后基地空气无法恢复。|Pack oxygen and a battery. Opening the hatch vents the habitat.|Возьмите кислород и батарею. Открытие люка выпустит воздух.
life.suit.don|按住 · 穿戴|HOLD · SUIT UP|УДЕРЖИВАТЬ · НАДЕТЬ
life.suit.sync|乘员服装同步联动|CREW SUIT SYNC|СИНХРОНИЗАЦИЯ СКАФАНДРОВ
life.suit.player|EVA 01 / 玩家宇航服|EVA 01 / PLAYER SUIT|EVA 01 / СКАФАНДР ИГРОКА
life.suit.commander|EVA 02 / 指挥官宇航服|EVA 02 / COMMANDER SUIT|EVA 02 / СКАФАНДР КОМАНДИРА
life.suit.oxygen|宇航服氧气|SUIT OXYGEN|КИСЛОРОД СКАФАНДРА
life.suit.power|宇航服电量|SUIT BATTERY|БАТАРЕЯ СКАФАНДРА
life.suit.thermal|体温 {0}°C\n温控余量 {1}%|BODY {0}°C\nTHERMAL MARGIN {1}%|ТЕЛО {0}°C\nТЕПЛОВОЙ РЕЗЕРВ {1}%
life.hatch.title|气闸控制|AIRLOCK CONTROL|УПРАВЛЕНИЕ ШЛЮЗОМ
life.hatch.repair_required|门锁损坏 · {0}%\n用工具接触下方维修点|LOCK FAULT · {0}%\nHOLD TOOL AT SOCKET BELOW|ЗАМОК СЛОМАН · {0}%\nИНСТРУМЕНТ К ГНЕЗДУ ВНИЗУ
life.hatch.locked|先维修舱门|REPAIR HATCH FIRST|СНАЧАЛА РЕМОНТ
life.hatch.tool_point|门锁维修点 · 手持工具|LOCK REPAIR · HOLD TOOL|РЕМОНТ ЗАМКА · ИНСТРУМЕНТ
life.hatch.repair.ready|等待工具接触|WAITING FOR TOOL|ПОДНЕСИТЕ ИНСТРУМЕНТ
life.hatch.repair.working|正在维修 · {0}%|REPAIRING · {0}%|РЕМОНТ · {0}%
life.hatch.repair.paused|维修暂停 · {0}%|REPAIR PAUSED · {0}%|РЕМОНТ ПРИОСТАНОВЛЕН · {0}%
life.hatch.repair.complete|门锁修复完成|LOCK REPAIRED|ЗАМОК ПОЧИНЕН
life.hatch.serial|舱门 01 · 手动泄压|HATCH 01 · MANUAL VENT|ЛЮК 01 · СБРОС ДАВЛЕНИЯ
life.hatch.warning|未穿宇航服\n开门将立即排空氧气！|NO SUIT\nOPENING VENTS ALL OXYGEN!|НЕТ СКАФАНДРА\nОТКРЫТИЕ ВЫПУСТИТ КИСЛОРОД!
life.hatch.suited|宇航服已穿戴\n确认补给后打开舱门|SUIT SEALED\nCHECK SUPPLIES BEFORE OPENING|СКАФАНДР ГОТОВ\nПРОВЕРЬТЕ ЗАПАСЫ
life.hatch.open|舱门开启\n氧气 0% · 温度失控|HATCH OPEN\nOXYGEN 0% · THERMAL CONTROL LOST|ЛЮК ОТКРЫТ\nКИСЛОРОД 0% · ТЕРМОКОНТРОЛЬ ОТКЛЮЧЁН
life.hatch.release|打开舱门 / 泄压|OPEN HATCH / VENT|ОТКРЫТЬ / СБРОСИТЬ ДАВЛЕНИЕ
life.hatch.vent_note|操作后无法重新加压|REPRESSURIZATION UNAVAILABLE|ПОВТОРНАЯ ГЕРМЕТИЗАЦИЯ НЕДОСТУПНА
life.hud.route_hatch|前往气闸 · {0} m|AIRLOCK · {0} m|К ШЛЮЗУ · {0} м
life.hud.route_ship|前往飞船 · {0} m|LANDER · {0} m|К КОРАБЛЮ · {0} м
life.hud.nominal|生命保障正常 · 检查补给|LIFE SUPPORT NOMINAL · CHECK SUPPLIES|СИСТЕМЫ В НОРМЕ · ПРОВЕРЬТЕ ЗАПАСЫ
life.hud.hypoxia|缺氧！剩余 {0} · 立即补氧|NO OXYGEN! {0} · REFILL NOW|НЕТ КИСЛОРОДА! {0} · ПОПОЛНИТЕ ЗАПАС
life.hud.battery|电量偏低 · 尽快更换电池|LOW BATTERY · REPLACE SOON|НИЗКИЙ ЗАРЯД · ЗАМЕНИТЕ БАТАРЕЮ
life.hud.oxygen|氧气偏低 · 尽快补充|LOW OXYGEN · REFILL SOON|МАЛО КИСЛОРОДА · ПОПОЛНИТЕ ЗАПАС
life.use.oxygen|使用携带氧气瓶|USE CARRIED OXYGEN|ИСПОЛЬЗОВАТЬ КИСЛОРОД
life.use.battery|更换携带电池|USE CARRIED BATTERY|ЗАМЕНИТЬ БАТАРЕЮ
life.wrist.title|机械补给阀|MANUAL SUPPLY VALVES|РУЧНАЯ ПОДАЧА
life.wrist.oxygen|补氧|O2|O2
life.wrist.battery|换电|PWR|ПИТ.
life.board.suit_required|必须先穿戴宇航服才能登船|Put on your suit before boarding.|Перед посадкой наденьте скафандр.
life.board.hatch_required|先维修并开启基地舱门|Repair and open the base airlock first.|Сначала почините и откройте шлюз базы.
life.feedback.ready|检查氧气与电量，先穿宇航服。|Check reserves and put on your suit.|Проверьте запасы и наденьте скафандр.
life.feedback.no_suit|需要先穿戴宇航服。|A suit is required.|Сначала наденьте скафандр.
life.feedback.suited|穿戴完成，指挥官已同步穿戴。|Suit sealed. Commander suited too.|Скафандр готов. Командир тоже экипирован.
life.feedback.vented|基地已泄压。|Habitat vented.|База разгерметизирована.
life.feedback.oxygen|氧气瓶已接入。|Oxygen refill connected.|Кислород подключён.
life.feedback.battery|电池已更换。|Battery replaced.|Батарея заменена.
life.cabin.resources|生命保障 / 储备|LIFE SUPPORT / RESERVES|ЖИЗНЕОБЕСПЕЧЕНИЕ
life.thermal_reserve|温控保障 / 供电余量|THERMAL / POWER RESERVE|ТЕРМОКОНТРОЛЬ / ПИТАНИЕ
life.cabin.temperature|舱温 {0}°C|CABIN {0}°C|В ОТСЕКЕ {0}°C
life.cabin.crew|指挥官 {0} · 健康 {1}%  /  基地剩余 {2}|COMMANDER {0} · HEALTH {1}%  /  BASE {2}|КОМАНДИР {0} · ЗДОРОВЬЕ {1}%  /  БАЗА {2}
life.dock.title|前视光学 / 交会对接|FORWARD OPTICS / RENDEZVOUS|ПЕРЕДНЯЯ КАМЕРА / СТЫКОВКА
life.dock.telemetry|距离 {0} m   接近 {1} m/s\n横向 {2} m   对准 {3}°|RANGE {0} m   CLOSURE {1} m/s\nLATERAL {2} m   ALIGN {3}°|ДИСТ. {0} м   СБЛИЖ. {1} м/с\nСМЕЩ. {2} м   УГОЛ {3}°
life.dock.axes|X {0}   Y {1}   俯仰 {2}°   偏航 {3}°\n主燃料 {4}% / 姿控 {5}%|X {0}   Y {1}   PITCH {2}°   YAW {3}°\nMAIN {4}% / RCS {5}%|X {0}   Y {1}   ТАНГАЖ {2}°   КУРС {3}°\nТОПЛИВО {4}% / RCS {5}%
life.dock.manual|对准目标菱形 · 低速接近|ALIGN TARGET DIAMOND · APPROACH SLOWLY|СОВМЕСТИТЕ РОМБ · СБЛИЖАЙТЕСЬ МЕДЛЕННО
life.dock.capture_ready|捕获窗口有效 · 可辅助对接|CAPTURE WINDOW · ASSIST AVAILABLE|ОКНО ЗАХВАТА · ПОМОЩЬ ДОСТУПНА
life.dock.capturing|正在捕获 · 保持稳定|CAPTURING · HOLD STEADY|ЗАХВАТ · ДЕРЖИТЕ КУРС
life.flight.circularize|到达入轨点 · 执行圆化点火|INSERTION POINT · CIRCULARIZE|ТОЧКА ВЫХОДА · КРУГОВАЯ ОРБИТА
life.flight.checklist|外部摄像机在线 · 完成启动程序|OPTICS ONLINE · COMPLETE STARTUP|КАМЕРА ВКЛЮЧЕНА · ВЫПОЛНИТЕ ЗАПУСК
life.key.oxygen|补氧|O2|O2
life.key.battery|换电|PWR|ПИТ.
life.key.repair|修漏|REPAIR|РЕМОНТ
life.key.medical|治疗|MED|ЛЕЧ.
life.key.brake|制动|BRAKE|ТОРМОЗ
life.key.boost|助推|BOOST|РАЗГОН
life.key.crew|乘员医疗|CREW MED|ЭКИПАЖ
life.key.orbit|入轨|ORBIT|ОРБИТА
life.failure.Suffocation|氧气耗尽，玩家窒息。|Oxygen exhausted. You suffocated.|Кислород закончился. Вы задохнулись.
life.failure.Hypothermia|温控失效，月夜低温致死。|Thermal control lost. Fatal cold exposure.|Термоконтроль отказал. Смертельное переохлаждение.
life.failure.Hyperthermia|温控失效，月昼高温致死。|Thermal control lost. Fatal heat exposure.|Термоконтроль отказал. Смертельный перегрев.
lifeground.briefing.instructions|先查看环境读数。开始后，找到右侧墙上的宇航服，备好氧气瓶与电池。|Check reserves. After starting, find the suit on the right wall. Pack oxygen and a battery.|Проверьте запасы. После старта найдите скафандр справа. Возьмите кислород и батарею.
lifeground.repair.instructions|穿服、补给并修好门锁。开门后指挥官跟随撤离；修复氧气设备可增加撤离时间。|Suit up, pack supplies and repair the lock. Opening starts evacuation with the commander. Oxygen repair adds time.|Скафандр, запасы, ремонт замка. Откройте люк — командир пойдёт за вами. Ремонт кислорода даёт время.
lifeground.stabilized.instructions|维修已稳定。收纳补给，确认宇航服电量，准备撤离。|Repair stabilized. Pack supplies and check suit battery before evacuation.|Ремонт завершён. Возьмите запасы и проверьте батарею перед эвакуацией.
lifeground.evacuation.instructions.repaired|穿服、修门后开门，指挥官自动跟随。到飞船旁等待他，再点击登舱。|Suit up, repair and open the hatch. The commander follows. Wait for him beside the ship, then board.|Наденьте скафандр, почините и откройте люк. Командир идёт за вами. Ждите его у корабля и садитесь.
lifeground.evacuation.instructions.unrepaired|氧气设备未修复。穿服、修门后开门撤离；到船旁等指挥官，再点击登舱。|Oxygen unit unrepaired. Suit up, repair the lock and open. Wait for the commander beside the ship, then board.|Кислородный блок сломан. Скафандр, ремонт замка, выход. Ждите командира у корабля и садитесь.
lifeground.completed.instructions|已进入飞船，使用实体控制台启动。|Boarded. Use the cockpit controls to launch.|Вы на борту. Запустите корабль с пульта.
lifeground.failed.instructions|本次撤离失败，查看结果后重试。|Evacuation failed. Review the result and retry.|Эвакуация не удалась. Посмотрите результат и повторите.'''

path = Path('Assets/_LunarEscape/Localization/repair-texts.json')
data = json.loads(path.read_text(encoding='utf-8-sig'))
existing = {row['key']: row for row in data['entries']}
for line in rows.splitlines():
    key, zh, en, ru = line.split('|')
    entry = dict(zip(('key', 'zh', 'en', 'ru'), (key, zh.replace('\\n','\n'), en.replace('\\n','\n'), ru.replace('\\n','\n'))))
    if key in existing: existing[key].update(entry)
    else: data['entries'].append(entry)
path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(f'Localization: {len(data["entries"])} entries')
