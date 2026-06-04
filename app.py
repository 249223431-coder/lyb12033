from flask import Flask
from config import Config
from models import db
from flask_login import LoginManager


def create_app():
    app = Flask(__name__)
    app.config.from_object(Config)

    db.init_app(app)

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

    from blueprints.main import main_bp
    app.register_blueprint(main_bp)

    with app.app_context():
        db.create_all()
        _init_default_data()

    return app


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


if __name__ == '__main__':
    import sys
    app = create_app()
    if '--dev' in sys.argv:
        app.run(host='0.0.0.0', port=3000, debug=True)
    else:
        from waitress import serve
        print(' * 使用Waitress生产模式启动...')
        serve(app, host='0.0.0.0', port=3000, threads=4, max_request_body_size=536870912)
