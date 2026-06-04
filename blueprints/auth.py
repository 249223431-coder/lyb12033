from flask import Blueprint, render_template, request, redirect, url_for, flash, jsonify
from flask_login import login_user, logout_user, login_required, current_user
from models import db, User
from functools import wraps

auth_bp = Blueprint('auth', __name__)


def admin_required(f):
    @wraps(f)
    def decorated_function(*args, **kwargs):
        if not current_user.is_authenticated or not current_user.is_admin:
            flash('需要管理员权限', 'error')
            return redirect(url_for('main.index'))
        return f(*args, **kwargs)
    return decorated_function


@auth_bp.route('/login', methods=['GET', 'POST'])
def login():
    if current_user.is_authenticated:
        return redirect(url_for('main.index'))
    if request.method == 'POST':
        username = request.form.get('username', '').strip()
        password = request.form.get('password', '')
        user = User.query.filter_by(username=username).first()
        if user and user.check_password(password):
            if not user.is_active:
                flash('账号已被禁用，请联系管理员', 'error')
            else:
                login_user(user, remember=True)
                next_page = request.args.get('next')
                return redirect(next_page or url_for('main.index'))
        else:
            flash('用户名或密码错误', 'error')
    return render_template('login.html')


@auth_bp.route('/register', methods=['GET', 'POST'])
def register():
    if current_user.is_authenticated:
        return redirect(url_for('main.index'))
    if request.method == 'POST':
        username = request.form.get('username', '').strip()
        password = request.form.get('password', '')
        confirm = request.form.get('confirm_password', '')
        real_name = request.form.get('real_name', '').strip()
        phone = request.form.get('phone', '').strip()

        if not all([username, password, real_name, phone]):
            flash('请填写所有必填字段', 'error')
        elif len(password) < 6:
            flash('密码至少6位', 'error')
        elif password != confirm:
            flash('两次密码不一致', 'error')
        elif User.query.filter_by(username=username).first():
            flash('用户名已存在', 'error')
        else:
            user = User(
                username=username,
                real_name=real_name,
                phone=phone,
                role='member',
                is_admin=False
            )
            user.set_password(password)
            db.session.add(user)
            db.session.commit()
            flash('注册成功！请使用管理员分配的账号登录，或联系管理员开通权限', 'success')
            return redirect(url_for('auth.login'))
    return render_template('register.html')


@auth_bp.route('/logout')
@login_required
def logout():
    logout_user()
    return redirect(url_for('auth.login'))


@auth_bp.route('/profile', methods=['GET', 'POST'])
@login_required
def profile():
    if request.method == 'POST':
        current_user.real_name = request.form.get('real_name', current_user.real_name)
        current_user.phone = request.form.get('phone', current_user.phone)

        new_password = request.form.get('new_password', '')
        if new_password:
            if len(new_password) < 6:
                flash('密码至少6位', 'error')
            else:
                current_user.set_password(new_password)
                flash('密码修改成功', 'success')

        db.session.commit()
        flash('个人信息已更新', 'success')
        return redirect(url_for('auth.profile'))
    return render_template('profile.html')
