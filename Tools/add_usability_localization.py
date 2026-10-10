import json
from pathlib import Path

path = Path('Assets/_LunarEscape/Localization/repair-texts.json')
table = json.loads(path.read_text(encoding='utf-8-sig'))
rows = '''cargo.pack.ready|松手收纳|RELEASE TO STOW|ОТПУСТИТЕ В СУМКУ
life.dock.complete|对接完成 · 接口已锁定|DOCKED · PORT LOCKED|СТЫКОВКА ЗАВЕРШЕНА
life.dock.capturing_progress|接口捕获中 · {0}%|PORT CAPTURE · {0}%|ЗАХВАТ ПОРТА · {0}%
life.dock.assist_disabled|辅助已关闭 · 手动对准接口|ASSIST OFF · ALIGN MANUALLY|ПОМОЩЬ ВЫКЛ. · СОВМЕСТИТЕ ПОРТЫ
life.dock.front|移到接口正前方 · 不要贴近舱体|MOVE IN FRONT OF THE PORT|ПЕРЕЙДИТЕ ПЕРЕД СТЫКОВОЧНЫМ ПОРТОМ
life.dock.range|接近黄色接口环 · 12米内开始辅助|APPROACH PORT RING · ASSIST WITHIN 12 m|К КОЛЬЦУ ПОРТА · ПОМОЩЬ В ПРЕДЕЛАХ 12 м
life.dock.offset|减小横向偏差 · 对准黄色环|REDUCE OFFSET · AIM AT PORT RING|УМЕНЬШИТЕ СМЕЩЕНИЕ К КОЛЬЦУ ПОРТА
life.dock.angle|调整俯仰与偏航 · 对正接口|ADJUST PITCH / YAW TO ALIGN|ВЫРОВНЯЙТЕ ТАНГАЖ И РЫСКАНИЕ
life.dock.brake|按住制动 · 降低速度与旋转|HOLD BRAKE · SLOW TRANSLATION / SPIN|ТОРМОЗИТЕ · СНИЗЬТЕ СКОРОСТЬ И ВРАЩЕНИЕ
life.dock.release|松开推力按钮 · 让辅助接管|RELEASE THRUST TO LET ASSIST TAKE OVER|ОТПУСТИТЕ ТЯГУ ДЛЯ АВТОСБЛИЖЕНИЯ
life.dock.aligning|辅助已接管 · 减速并对齐|ASSIST ACTIVE · BRAKING / ALIGNING|ПОМОЩЬ АКТИВНА · ТОРМОЖЕНИЕ / ВЫРАВНИВАНИЕ
life.dock.approaching|辅助已接管 · 正在缓慢靠近|ASSIST ACTIVE · SLOW APPROACH|ПОМОЩЬ АКТИВНА · МЕДЛЕННОЕ СБЛИЖЕНИЕ'''
for line in rows.splitlines():
    key, zh, en, ru = line.split('|')
    entry = next((e for e in table['entries'] if e['key'] == key), None)
    if entry is None:
        entry = {}; table['entries'].append(entry)
    entry.update(key=key, zh=zh, en=en, ru=ru)
path.write_text(json.dumps(table, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
