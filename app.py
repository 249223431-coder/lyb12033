from flask import Flask
from config import Config
from models import db
from flask_login import LoginManager
from sqlalchemy import event


def _enable_wal(dbapi_connection, connection_record):
    """启用SQLite WAL模式，允许并发读写"""
    import sqlite3
    if isinstance(dbapi_connection, sqlite3.Connection):
        cursor = dbapi_connection.cursor()
        cursor.execute("PRAGMA journal_mode=WAL")
        cursor.execute("PRAGMA synchronous=NORMAL")
        cursor.execute("PRAGMA cache_size=-8000")  # 8MB缓存
        cursor.execute("PRAGMA temp_store=MEMORY")
        cursor.close()


def create_app():
    app = Flask(__name__)
    app.config.from_object(Config)

    db.init_app(app)

    with app.app_context():
        event.listen(db.engine, 'connect', _enable_wal)

    login_manager = LoginManager()
    login_manager.init_app(app)
    login_manager.login_view = 'auth.login'
    login_manager.login_message = '请先登录'

    from models import User

    @login_manager.user_loader
    def load_user(user_id):
        return User.query.get(int(user_id))

    from blueprints.auth import auth_bp
    from blueprints.attendance import attendance_bp
    from blueprints.documents import documents_bp
    from blueprints.inspection import inspection_bp
    from blueprints.notifications import notifications_bp
    from blueprints.dashboard import dashboard_bp
    from blueprints.spare_parts import spare_parts_bp
    from blueprints.admin import admin_bp
    from blueprints.export import export_bp
    from blueprints.data_search import data_search_bp
    from blueprints.motor import motor_bp
    from blueprints.overtime import overtime_bp
    from blueprints.shutdown import shutdown_bp

    app.register_blueprint(auth_bp, url_prefix='/auth')
    app.register_blueprint(attendance_bp, url_prefix='/attendance')
    app.register_blueprint(documents_bp, url_prefix='/documents')
    app.register_blueprint(inspection_bp, url_prefix='/inspection')
    app.register_blueprint(notifications_bp, url_prefix='/notifications')
    app.register_blueprint(dashboard_bp, url_prefix='/dashboard')
    app.register_blueprint(spare_parts_bp, url_prefix='/spare_parts')
    app.register_blueprint(admin_bp, url_prefix='/admin')
    app.register_blueprint(export_bp, url_prefix='/export')
    app.register_blueprint(data_search_bp, url_prefix='/data_search')
    app.register_blueprint(motor_bp, url_prefix='/motor')
    app.register_blueprint(overtime_bp, url_prefix='/overtime')
    app.register_blueprint(shutdown_bp, url_prefix='/shutdown')

    from blueprints.main import main_bp
    app.register_blueprint(main_bp)

    with app.app_context():
        db.create_all()
        _migrate_db(app)
        _init_default_data()
        _auto_backup()

    return app


def _auto_backup():
    """每天首次启动时自动备份数据库"""
    import os
    import time
    import shutil
    today = time.strftime('%Y%m%d')
    src = os.path.join(os.path.dirname(__file__), 'instance', 'team.db')
    if os.path.exists(src):
        backup_dir = r'D:\team_backups'
        os.makedirs(backup_dir, exist_ok=True)
        dst = os.path.join(backup_dir, f'team_auto_{today}.db')
        if not os.path.exists(dst):
            shutil.copy2(src, dst)
            print(f' * [自动备份] 数据库已备份到 {dst}')


def _init_default_data():
    from models import User, DocumentCategory
    if User.query.filter_by(username='admin').first() is None:
        admin = User(
            username='admin',
            real_name='系统管理员',
            phone='13800000000',
            role='admin',
            is_admin=True,
            is_active=True
        )
        admin.set_password('admin123')
        db.session.add(admin)
        db.session.commit()

    categories = ['图纸', '维修手册', '选型手册', '操作手册', 'SOP', '表单', '图片存档']
    for i, name in enumerate(categories):
        if DocumentCategory.query.filter_by(name=name).first() is None:
            db.session.add(DocumentCategory(name=name, sort_order=i + 1, description=f'{name}资料库'))
    db.session.commit()


def _migrate_db(app):
    """安全迁移数据库结构，不丢失数据"""
    from sqlalchemy import text, inspect
    with app.app_context():
        inspector = inspect(db.engine)
        # purchase_requisitions表添加part_code列
        if 'purchase_requisitions' in inspector.get_table_names():
            cols = [c['name'] for c in inspector.get_columns('purchase_requisitions')]
            if 'part_code' not in cols:
                with db.engine.connect() as conn:
                    conn.execute(text("ALTER TABLE purchase_requisitions ADD COLUMN part_code VARCHAR(100) DEFAULT ''"))
                    conn.commit()
                print(' * [迁移] purchase_requisitions 表添加 part_code 列')


if __name__ == '__main__':
    import sys
    app = create_app()
    if '--dev' in sys.argv:
        app.run(host='0.0.0.0', port=3000, debug=True)
    else:
        from waitress import serve
        print(' * 使用Waitress生产模式启动...')
        serve(app, host='0.0.0.0', port=3000, threads=16, connection_limit=100, channel_timeout=120, max_request_body_size=536870912)
