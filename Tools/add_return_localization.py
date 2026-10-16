"""给返航尾声补充三语文案；重复运行只更新同名键。"""
import json
from pathlib import Path

path=Path('Assets/_LunarEscape/Localization/repair-texts.json')
table=json.loads(path.read_text(encoding='utf-8-sig'))
entries=[
  {
    "key": "return.title",
    "zh": "地球返航 · 轨道返回舱",
    "en": "EARTH RETURN · ORBITAL CAPSULE",
    "ru": "ВОЗВРАЩЕНИЕ НА ЗЕМЛЮ"
  },
  {
    "key": "return.transfer.detail",
    "zh": "对接口已锁定。确认后转入轨道返回舱，已获救的指挥官会一同转舱。",
    "en": "Port locked. Transfer to the return capsule. The rescued commander will follow you.",
    "ru": "Порт зафиксирован. Перейдите в спускаемую капсулу. Спасённый командир последует за вами."
  },
  {
    "key": "return.action.transfer",
    "zh": "确认转入返回舱",
    "en": "TRANSFER TO RETURN CAPSULE",
    "ru": "ПЕРЕЙТИ В СПУСКАЕМУЮ КАПСУЛУ"
  },
  {
    "key": "return.action.separate",
    "zh": "关闭顶舱门 · 分离上升器",
    "en": "CLOSE TOP HATCH · RELEASE ASCENT STAGE",
    "ru": "ЗАКРЫТЬ ЛЮК · ОТДЕЛИТЬ ВЗЛЁТНУЮ СТУПЕНЬ"
  },
  {
    "key": "return.action.depart",
    "zh": "确认点火 · 返回地球",
    "en": "IGNITE · DEPART FOR EARTH",
    "ru": "ПУСК ДВИГАТЕЛЯ · К ЗЕМЛЕ"
  },
  {
    "key": "return.pause",
    "zh": "暂停动画",
    "en": "PAUSE SEQUENCE",
    "ru": "ПАУЗА"
  },
  {
    "key": "return.resume",
    "zh": "继续返航",
    "en": "RESUME RETURN",
    "ru": "ПРОДОЛЖИТЬ ВОЗВРАЩЕНИЕ"
  },
  {
    "key": "return.paused",
    "zh": "返航动画已暂停。准备好后继续。",
    "en": "Sequence paused. Resume when ready.",
    "ru": "Полёт приостановлен. Продолжите, когда будете готовы."
  },
  {
    "key": "return.compressed",
    "zh": "默认油门航程约60秒 · 开伞、抛底不暂停下降",
    "en": "ABOUT 60 s AT DEFAULT THROTTLE · DESCENT NEVER WAITS FOR ACTIONS",
    "ru": "ОКОЛО 60 с ПРИ ШТАТНОЙ ТЯГЕ · СНИЖЕНИЕ НЕ ЖДЁТ ДЕЙСТВИЙ"
  },
  {
    "key": "return.crew.all",
    "zh": "玩家、指挥官与轨道驾驶员已在返回舱",
    "en": "YOU, COMMANDER AND ORBITAL PILOT ABOARD",
    "ru": "ВЫ, КОМАНДИР И ПИЛОТ НА БОРТУ"
  },
  {
    "key": "return.crew.absent",
    "zh": "玩家与轨道驾驶员在舱 · 指挥官未能随行",
    "en": "YOU AND ORBITAL PILOT ABOARD · COMMANDER ABSENT",
    "ru": "ВЫ И ПИЛОТ НА БОРТУ · КОМАНДИР НЕ С ВАМИ"
  },
  {
    "key": "return.complete",
    "zh": "已返回地球 · 等待海上回收",
    "en": "BACK ON EARTH · AWAITING RECOVERY",
    "ru": "НА ЗЕМЛЕ · ОЖИДАНИЕ ЭВАКУАЦИИ"
  },
  {
    "key": "return.phase.Inactive",
    "zh": "返航待命",
    "en": "RETURN STANDBY",
    "ru": "ОЖИДАНИЕ ВОЗВРАЩЕНИЯ"
  },
  {
    "key": "return.detail.Inactive",
    "zh": "完成对接后开始返航。",
    "en": "Dock to begin the return.",
    "ru": "Для возвращения завершите стыковку."
  },
  {
    "key": "return.phase.AwaitingTransfer",
    "zh": "对接完成 · 准备转舱",
    "en": "DOCKED · READY TO TRANSFER",
    "ru": "СТЫКОВКА · ГОТОВНОСТЬ К ПЕРЕХОДУ"
  },
  {
    "key": "return.detail.AwaitingTransfer",
    "zh": "确认转入轨道返回舱。",
    "en": "Confirm transfer to the return capsule.",
    "ru": "Подтвердите переход в капсулу."
  },
  {
    "key": "return.phase.TransferFade",
    "zh": "正在转舱",
    "en": "TRANSFERRING",
    "ru": "ПЕРЕХОД"
  },
  {
    "key": "return.detail.TransferFade",
    "zh": "保持舒适姿势，即将进入轨道返回舱。",
    "en": "Remain comfortable during transfer.",
    "ru": "Сохраняйте удобное положение во время перехода."
  },
  {
    "key": "return.phase.CrewTransfer",
    "zh": "乘员转舱就位",
    "en": "CREW TRANSFER",
    "ru": "ПЕРЕХОД ЭКИПАЖА"
  },
  {
    "key": "return.detail.CrewTransfer",
    "zh": "指挥官从顶部对接通道进入，正在就位。",
    "en": "Commander entering through the top docking tunnel.",
    "ru": "Командир входит через верхний стыковочный тоннель."
  },
  {
    "key": "return.phase.AwaitingSeparation",
    "zh": "乘员就位 · 等待分离",
    "en": "CREW SECURED · SEPARATION READY",
    "ru": "ЭКИПАЖ ГОТОВ · ОТДЕЛЕНИЕ"
  },
  {
    "key": "return.detail.AwaitingSeparation",
    "zh": "确认关闭转舱门，并与登月舱分离。",
    "en": "Close the transfer hatch and release the lunar module.",
    "ru": "Закройте переходный люк и отделите лунный модуль."
  },
  {
    "key": "return.phase.Separating",
    "zh": "上升器分离",
    "en": "ASCENT STAGE SEPARATION",
    "ru": "ОТДЕЛЕНИЕ ВЗЛЁТНОЙ СТУПЕНИ"
  },
  {
    "key": "return.detail.Separating",
    "zh": "返回舱和服务推进舱保持连接，一起与上升器分离。",
    "en": "The capsule and service module remain connected as the ascent stage separates.",
    "ru": "Капсула и служебный модуль остаются вместе. Взлётная ступень отделяется."
  },
  {
    "key": "return.phase.AwaitingDeparture",
    "zh": "分离完成 · 返地准备",
    "en": "SEPARATED · READY FOR EARTH",
    "ru": "МОДУЛЬ ОТДЕЛЁН · ПУТЬ К ЗЕМЛЕ"
  },
  {
    "key": "return.detail.AwaitingDeparture",
    "zh": "组合飞船已与上升器分离。确认点火前往地球。",
    "en": "The complete return spacecraft is free of the ascent stage. Ignite for Earth.",
    "ru": "Возвращаемый корабль отделён от взлётной ступени. Включите двигатель для полёта к Земле."
  },
  {
    "key": "return.phase.DepartureBurn",
    "zh": "返地发动机点火",
    "en": "TRANS-EARTH BURN",
    "ru": "РАЗГОН К ЗЕМЛЕ"
  },
  {
    "key": "return.detail.DepartureBurn",
    "zh": "油门控制点火进度；0% 暂停推进。侧屏显示离月外景。",
    "en": "Throttle controls burn rate; 0% holds thrust. Watch departure on the side monitor.",
    "ru": "Тяга задаёт темп разгона; 0% останавливает его. Отлёт виден на боковом экране."
  },
  {
    "key": "return.phase.Coast",
    "zh": "地月转移航程",
    "en": "COASTING TO EARTH",
    "ru": "ПЕРЕЛЁТ К ЗЕМЛЕ"
  },
  {
    "key": "return.detail.Coast",
    "zh": "三天航程正在压缩播放，地球同步加速自转。",
    "en": "Three-day coast in time compression. Earth rotates at the same time scale.",
    "ru": "Трёхдневный перелёт ускорен. Вращение Земли ускорено соответственно."
  },
  {
    "key": "return.phase.EarthApproach",
    "zh": "地球捕获 · 制动点火",
    "en": "EARTH CAPTURE · BRAKING BURN",
    "ru": "ЗАХВАТ ЗЕМЛЁЙ · ТОРМОЖЕНИЕ"
  },
  {
    "key": "return.detail.EarthApproach",
    "zh": "制动进入地球停泊轨道。时间加速逐渐结束，准备手动离轨。",
    "en": "Braking into Earth orbit. Time acceleration ends; prepare to deorbit.",
    "ru": "Торможение до околоземной орбиты. Подготовьтесь к сходу с орбиты."
  },
  {
    "key": "return.phase.ServiceSeparation",
    "zh": "再入准备 · 分离服务推进舱",
    "en": "ENTRY PREPARATION · SERVICE SEPARATION",
    "ru": "ПОДГОТОВКА К ВХОДУ · ОТДЕЛЕНИЕ СЛУЖЕБНОГО МОДУЛЯ"
  },
  {
    "key": "return.detail.ServiceSeparation",
    "zh": "返回舱与服务推进舱脱离，隔热大底朝向地球进入大气。",
    "en": "The capsule releases the service module, then descends heat-shield first.",
    "ru": "Капсула отделяет служебный модуль и входит в атмосферу теплозащитой вперёд."
  },
  {
    "key": "return.phase.Entry",
    "zh": "大气层再入",
    "en": "ATMOSPHERIC ENTRY",
    "ru": "ВХОД В АТМОСФЕРУ"
  },
  {
    "key": "return.detail.Entry",
    "zh": "舷窗边缘出现热流。返回舱正在减速。",
    "en": "Heated flow passes the window. The capsule slows down.",
    "ru": "У окна виден нагретый поток. Капсула тормозит."
  },
  {
    "key": "return.phase.Parachutes",
    "zh": "主降落伞展开",
    "en": "MAIN PARACHUTES DEPLOYED",
    "ru": "ОСНОВНЫЕ ПАРАШЮТЫ РАСКРЫТЫ"
  },
  {
    "key": "return.detail.Parachutes",
    "zh": "伞包在舱外释放，伞绳拉直后充气。继续看高度，在1.0–0.4 km内抛底。",
    "en": "External packs release, lines extend and canopies inflate. Release the shield at 1.0–0.4 km.",
    "ru": "Наружные укладки раскрываются, стропы натягиваются, купола наполняются. Сбросьте щит на 1,0–0,4 км."
  },
  {
    "key": "return.phase.Splashdown",
    "zh": "海面降落",
    "en": "SPLASHDOWN",
    "ru": "ПРИВОДНЕНИЕ"
  },
  {
    "key": "return.detail.Splashdown",
    "zh": "返回舱接近海面，即将落水。",
    "en": "Capsule approaching the ocean for splashdown.",
    "ru": "Капсула приближается к океану для приводнения."
  },
  {
    "key": "return.phase.Complete",
    "zh": "已返回地球",
    "en": "BACK ON EARTH",
    "ru": "ВОЗВРАЩЕНИЕ НА ЗЕМЛЮ"
  },
  {
    "key": "return.detail.Complete",
    "zh": "保持在舱内，等待海上回收队。",
    "en": "Remain aboard for the recovery team.",
    "ru": "Оставайтесь в капсуле и ждите спасателей."
  },
  {
    "key": "return.detail.solo",
    "zh": "轨道驾驶员已在舱。指挥官未能随行，正在准备分离。",
    "en": "The orbital pilot is aboard. The commander could not join. Preparing to separate.",
    "ru": "Пилот на борту. Командир не смог присоединиться. Подготовка к отделению."
  },
  {
    "key": "return.phase.OrbitHold",
    "zh": "地球轨道 · 手动设置再入",
    "en": "EARTH ORBIT · SET ENTRY ANGLE",
    "ru": "ОРБИТА ЗЕМЛИ · УГОЛ ВХОДА"
  },
  {
    "key": "return.detail.OrbitHold",
    "zh": "调整下降夹角，再确认离轨点火。",
    "en": "Adjust the descent angle, then confirm the deorbit burn.",
    "ru": "Задайте угол снижения и подтвердите торможение."
  },
  {
    "key": "return.corridor.safe",
    "zh": "目标走廊 5.5°–7.5°：当前角度安全。确认离轨后将锁定轨道。",
    "en": "CORRIDOR 5.5°–7.5°: safe. Confirm to commit this trajectory.",
    "ru": "КОРИДОР 5.5°–7.5°: безопасно. Подтвердите траекторию."
  },
  {
    "key": "return.corridor.shallow",
    "zh": "角度过浅！会跳出大气，错过回收。增加角度进入 5.5°–7.5° 走廊。",
    "en": "TOO SHALLOW: skip-out risk. Increase angle into 5.5°–7.5°.",
    "ru": "СЛИШКОМ ПОЛОГО: риск отскока. Увеличьте угол до 5.5°–7.5°."
  },
  {
    "key": "return.corridor.steep",
    "zh": "角度过陡！高温和过载将破坏舱体。减小角度进入 5.5°–7.5° 走廊。",
    "en": "TOO STEEP: heat and overload. Reduce angle into 5.5°–7.5°.",
    "ru": "СЛИШКОМ КРУТО: перегрев и перегрузка. Уменьшите угол до 5.5°–7.5°."
  },
  {
    "key": "return.shallower",
    "zh": "减小 0.5°",
    "en": "− 0.5°",
    "ru": "− 0.5°"
  },
  {
    "key": "return.steeper",
    "zh": "增大 0.5°",
    "en": "+ 0.5°",
    "ru": "+ 0.5°"
  },
  {
    "key": "return.action.deorbit",
    "zh": "按当前角度 · 离轨点火",
    "en": "COMMIT ANGLE · DEORBIT",
    "ru": "ПОДТВЕРДИТЬ УГОЛ · СХОД С ОРБИТЫ"
  },
  {
    "key": "return.telemetry",
    "zh": "速度 {0} km/s\n高度 {1} km\n舱外温度 {2} °C\n热盾 {3} °C\n过载 {4} g · 结构 {5}%",
    "en": "SPEED {0} km/s\nALTITUDE {1} km\nEXTERIOR {2} °C\nSHIELD {3} °C\nLOAD {4} g · HULL {5}%",
    "ru": "СКОРОСТЬ {0} км/с\nВЫСОТА {1} км\nСНАРУЖИ {2} °C\nЩИТ {3} °C\nНАГРУЗКА {4} g · {5}%"
  },
  {
    "key": "return.phase.DeorbitBurn",
    "zh": "离轨点火 · 轨道已锁定",
    "en": "DEORBIT BURN · TRAJECTORY COMMITTED",
    "ru": "СХОД С ОРБИТЫ · ТРАЕКТОРИЯ ЗАДАНА"
  },
  {
    "key": "return.detail.DeorbitBurn",
    "zh": "按设定夹角降低近地点，即将分离服务舱。",
    "en": "Lowering perigee for the selected entry angle.",
    "ru": "Снижение перигея для выбранного угла входа."
  },
  {
    "key": "return.phase.Breakup",
    "zh": "过热与过载 · 舱体解体",
    "en": "OVERHEAT / OVERLOAD · BREAKUP",
    "ru": "ПЕРЕГРЕВ / ПЕРЕГРУЗКА · РАЗРУШЕНИЕ"
  },
  {
    "key": "return.detail.Breakup",
    "zh": "再入角度过陡，热盾和结构失效。",
    "en": "Entry too steep. Heat shield and structure have failed.",
    "ru": "Слишком крутой вход. Теплозащита и корпус разрушены."
  },
  {
    "key": "return.phase.SkipOut",
    "zh": "再入过浅 · 跳出大气",
    "en": "SHALLOW ENTRY · SKIP-OUT",
    "ru": "ПОЛОГИЙ ВХОД · ОТСКОК"
  },
  {
    "key": "return.detail.SkipOut",
    "zh": "减速不足，高度再次上升，已错过回收走廊。",
    "en": "Insufficient deceleration. Climbing beyond the recovery corridor.",
    "ru": "Недостаточное торможение. Капсула покидает коридор посадки."
  },
  {
    "key": "return.phase.Failed",
    "zh": "返航失败",
    "en": "RETURN FAILED",
    "ru": "ВОЗВРАЩЕНИЕ НЕ УДАЛОСЬ"
  },
  {
    "key": "return.detail.Failed",
    "zh": "重新开始任务，再次尝试安全再入。",
    "en": "Restart the mission and try a safe entry.",
    "ru": "Начните миссию заново и повторите вход."
  },
  {
    "key": "return.failure.steep",
    "zh": "返航失败 · 再入过陡，舱体解体",
    "en": "RETURN FAILED · STEEP ENTRY BREAKUP",
    "ru": "НЕУДАЧА · РАЗРУШЕНИЕ ПРИ КРУТОМ ВХОДЕ"
  },
  {
    "key": "return.failure.shallow",
    "zh": "返航失败 · 再入过浅，跳出大气",
    "en": "RETURN FAILED · SHALLOW SKIP-OUT",
    "ru": "НЕУДАЧА · ОТСКОК ОТ АТМОСФЕРЫ"
  },
  {
    "key": "return.failure.total",
    "zh": "最终任务未完成 · 无返航成绩",
    "en": "MISSION INCOMPLETE · NO RETURN SCORE",
    "ru": "МИССИЯ НЕ ЗАВЕРШЕНА · НЕТ ИТОГОВОЙ ОЦЕНКИ"
  },
  {
    "key": "return.failure.crew",
    "zh": "乘员未能安全返回地球",
    "en": "CREW DID NOT RETURN SAFELY",
    "ru": "ЭКИПАЖ НЕ ВЕРНУЛСЯ В БЕЗОПАСНОСТИ"
  },
  {
    "key": "return.failure.science",
    "zh": "科研载荷未完成回收",
    "en": "SCIENCE PAYLOAD NOT RECOVERED",
    "ru": "НАУЧНЫЙ ГРУЗ НЕ ВОЗВРАЩЁН"
  },
  {
    "key": "return.throttle",
    "zh": "油门 {0}%",
    "en": "THROTTLE {0}%",
    "ru": "ТЯГА {0}%"
  },
  {
    "key": "return.throttle.down",
    "zh": "− 10%",
    "en": "− 10%",
    "ru": "− 10%"
  },
  {
    "key": "return.throttle.up",
    "zh": "+ 10%",
    "en": "+ 10%",
    "ru": "+ 10%"
  },
  {
    "key": "return.direction",
    "zh": "再入夹角 {0}°\n航向偏转 {1}°",
    "en": "ENTRY ANGLE {0}°\nHEADING {1}°",
    "ru": "УГОЛ ВХОДА {0}°\nКУРС {1}°"
  },
  {
    "key": "return.turn.left",
    "zh": "左转 2°",
    "en": "LEFT 2°",
    "ru": "ВЛЕВО 2°"
  },
  {
    "key": "return.turn.right",
    "zh": "右转 2°",
    "en": "RIGHT 2°",
    "ru": "ВПРАВО 2°"
  },
  {
    "key": "return.deploy",
    "zh": "打开降落伞",
    "en": "OPEN PARACHUTES",
    "ru": "РАСКРЫТЬ ПАРАШЮТЫ"
  },
  {
    "key": "return.release",
    "zh": "抛隔热大底",
    "en": "RELEASE HEAT SHIELD",
    "ru": "СБРОСИТЬ ТЕПЛОЩИТ"
  },
  {
    "key": "return.monitor",
    "zh": "舱外追踪 · 实时画面",
    "en": "EXTERIOR TRACKING · LIVE",
    "ru": "ВНЕШНЯЯ КАМЕРА · ЭФИР"
  },
  {
    "key": "return.phase.AwaitingParachutes",
    "zh": "持续下降 · 监视开伞高度",
    "en": "DESCENDING · WATCH PARACHUTE ALTITUDE",
    "ru": "СНИЖЕНИЕ · СЛЕДИТЕ ЗА ВЫСОТОЙ"
  },
  {
    "key": "return.phase.AwaitingHeatShield",
    "zh": "持续下降 · 抛底高度窗口",
    "en": "DESCENDING · HEAT SHIELD WINDOW",
    "ru": "СНИЖЕНИЕ · ОКНО СБРОСА ЩИТА"
  },
  {
    "key": "return.phase.HeatShieldRelease",
    "zh": "隔热大底分离",
    "en": "HEAT SHIELD RELEASE",
    "ru": "ОТДЕЛЕНИЕ ТЕПЛОЩИТА"
  },
  {
    "key": "return.detail.AwaitingParachutes",
    "zh": "持续下降中。3.2–1.8 km 内打开降落伞，越过下限将高速坠落。",
    "en": "Keep descending. Open parachutes at 3.2–1.8 km. Missing the window causes a high-speed impact.",
    "ru": "Снижение продолжается. Раскройте парашюты на 3,2–1,8 км. Пропуск окна приведёт к удару."
  },
  {
    "key": "return.detail.AwaitingHeatShield",
    "zh": "主伞稳定，仍在下降。必须在1.0–0.4 km内抛隔热大底。",
    "en": "Canopies are stable. Descent continues. Release the shield within 1.0–0.4 km.",
    "ru": "Купола устойчивы. Снижение продолжается. Сбросьте теплозащиту на высоте 1,0–0,4 км."
  },
  {
    "key": "return.detail.HeatShieldRelease",
    "zh": "大底向下落离，返回舱继续下降。侧屏同时追踪伞组、舱体与大底。",
    "en": "Shield falls clear while the capsule keeps descending. The monitor tracks the full assembly.",
    "ru": "Щит падает вниз, капсула продолжает снижение. Боковой экран отслеживает спуск."
  },
  {
    "key": "return.phase.HardLanding",
    "zh": "返回速度过快 · 即将撞击",
    "en": "EXCESSIVE DESCENT SPEED · IMPACT",
    "ru": "СКОРОСТЬ СНИЖЕНИЯ ПРЕВЫШЕНА · УДАР"
  },
  {
    "key": "return.detail.HardLanding",
    "zh": "错过高度窗口，无法安全着陆。",
    "en": "Altitude window missed. A safe landing is no longer possible.",
    "ru": "Высотное окно пропущено. Безопасная посадка невозможна."
  },
  {
    "key": "return.failure.parachutes",
    "zh": "返航失败：错过开伞高度，速度过快撞击。",
    "en": "RETURN FAILED: parachute window missed; high-speed impact.",
    "ru": "ВОЗВРАЩЕНИЕ НЕ УДАЛОСЬ: окно раскрытия парашютов пропущено; удар на высокой скорости."
  },
  {
    "key": "return.failure.shield",
    "zh": "返航失败：错过抛底高度，着陆速度超限。",
    "en": "RETURN FAILED: heat-shield window missed; landing speed exceeded.",
    "ru": "ВОЗВРАЩЕНИЕ НЕ УДАЛОСЬ: окно сброса теплозащиты пропущено; скорость посадки превышена."
  },
  {
    "key": "return.altitude.title",
    "zh": "末段高度 · 4.2–0 km",
    "en": "DESCENT ALTITUDE · 4.2–0 km",
    "ru": "ВЫСОТА СПУСКА · 4,2–0 км"
  },
  {
    "key": "return.altitude.chute_range",
    "zh": "打开降落伞\n3.2–1.8 km",
    "en": "OPEN PARACHUTES\n3.2–1.8 km",
    "ru": "ПАРАШЮТЫ\n3,2–1,8 км"
  },
  {
    "key": "return.altitude.shield_range",
    "zh": "抛隔热大底\n1.0–0.4 km",
    "en": "RELEASE SHIELD\n1.0–0.4 km",
    "ru": "СБРОС ЩИТА\n1,0–0,4 км"
  },
  {
    "key": "return.altitude.chute_next",
    "zh": "持续下降 · 等待开伞高度",
    "en": "DESCENDING · CHUTE WINDOW AHEAD",
    "ru": "СНИЖЕНИЕ · ЖДИТЕ ОКНА ПАРАШЮТОВ"
  },
  {
    "key": "return.altitude.chute_now",
    "zh": "现在开伞！",
    "en": "OPEN PARACHUTES NOW!",
    "ru": "РАСКРОЙТЕ ПАРАШЮТЫ!"
  },
  {
    "key": "return.altitude.shield_next",
    "zh": "伞已开 · 等待抛底高度",
    "en": "CHUTES OPEN · SHIELD WINDOW AHEAD",
    "ru": "ПАРАШЮТЫ РАСКРЫТЫ · ЖДИТЕ СБРОСА"
  },
  {
    "key": "return.altitude.shield_now",
    "zh": "现在抛底！",
    "en": "RELEASE HEAT SHIELD NOW!",
    "ru": "СБРОСЬТЕ ТЕПЛОЗАЩИТУ!"
  },
  {
    "key": "return.altitude.landing",
    "zh": "准备着陆 · 保持系紧",
    "en": "LANDING · STAY STRAPPED IN",
    "ru": "ПОСАДКА · ОСТАВАЙТЕСЬ ПРИСТЁГНУТЫ"
  },
  {
    "key": "return.altitude.missed",
    "zh": "错过高度窗口 · 速度超限",
    "en": "WINDOW MISSED · SPEED EXCEEDED",
    "ru": "ОКНО ПРОПУЩЕНО · СКОРОСТЬ ПРЕВЫШЕНА"
  }
]
for entry in entries:
    old=next((e for e in table['entries'] if e['key']==entry['key']),None)
    if old is None:table['entries'].append(entry)
    else:old.update(entry)
path.write_text(json.dumps(table,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
