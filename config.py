import os

BASE_DIR = os.path.abspath(os.path.dirname(__file__))


class Config:
    SECRET_KEY = os.environ.get('SECRET_KEY') or 'team-management-secret-key-2024'
    SQLALCHEMY_DATABASE_URI = os.environ.get('DATABASE_URL') or \
        'sqlite:///' + os.path.join(BASE_DIR, 'instance', 'team.db')
    SQLALCHEMY_TRACK_MODIFICATIONS = False
    # SQLite WAL模式 + 连接池优化，支持并发读写
    SQLALCHEMY_ENGINE_OPTIONS = {
        'connect_args': {
            'check_same_thread': False,
            'timeout': 20,          # 等待锁的超时时间
        },
        'pool_size': 10,            # 连接池大小
        'pool_recycle': 300,        # 连接回收时间(秒)
        'pool_pre_ping': True,      # 使用前检查连接有效性
        'max_overflow': 10,         # 最大溢出连接数
    }
    UPLOAD_FOLDER = os.path.join(BASE_DIR, 'static', 'uploads')
    MAX_CONTENT_LENGTH = 500 * 1024 * 1024  # 500MB max upload
    ALLOWED_EXTENSIONS = {
        'pdf', 'doc', 'docx', 'xls', 'xlsx', 'ppt', 'pptx',
        'jpg', 'jpeg', 'png', 'gif', 'bmp', 'svg',
        'dwg', 'dxf', 'step', 'stp', 'igs', 'iges',
        'zip', 'rar', '7z', 'tar', 'gz',
        'txt', 'csv', 'mp4', 'avi', 'mov', 'mp3'
    }
    PLC_ENABLED = os.environ.get('PLC_ENABLED', 'false').lower() == 'true'
    PLC_HOST = os.environ.get('PLC_HOST', '192.168.1.100')
    PLC_RACK = int(os.environ.get('PLC_RACK', '0'))
    PLC_SLOT = int(os.environ.get('PLC_SLOT', '1'))
    PLC_TAGS = {
        'transformer_temp': {'area': 0x84, 'db': 1, 'start': 0, 'bit': 0},
        'mcc_temp': {'area': 0x84, 'db': 1, 'start': 2, 'bit': 0},
        'room_temp': {'area': 0x84, 'db': 1, 'start': 4, 'bit': 0},
        'room_humidity': {'area': 0x84, 'db': 1, 'start': 6, 'bit': 0},
    }
