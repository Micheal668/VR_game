"""兼容旧命令，统一使用包含手动开伞与侧屏的完整文本。"""
import runpy
from pathlib import Path
runpy.run_path(str(Path(__file__).with_name('add_return_localization.py')))
