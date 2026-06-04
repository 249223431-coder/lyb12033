from flask import Blueprint, render_template, request, redirect, url_for, flash
from flask_login import login_required, current_user
from models import db, User
from blueprints.auth import admin_required

admin_bp = Blueprint('admin', __name__)


@admin_bp.route('/')
@login_required
@admin_required
def index():
    user_count = User.query.count()
    active_count = User.query.filter_by(is_active=True).count()
    return render_template('admin/index.html', user_count=user_count, active_count=active_count)


@admin_bp.route('/users')
@login_required
@admin_required
def users():
    all_users = User.query.order_by(User.created_at.desc()).all()
    return render_template('admin/users.html', users=all_users)


@admin_bp.route('/users/add', methods=['POST'])
@login_required
@admin_required
def add_user():
    username = request.form.get('username', '').strip()
    password = request.form.get('password', '')
    real_name = request.form.get('real_name', '').strip()
    phone = request.form.get('phone', '').strip()
    role = request.form.get('role', 'member')

    if not all([username, password, real_name]):
        flash('请填写必填信息', 'error')
    elif User.query.filter_by(username=username).first():
        flash('用户名已存在', 'error')
    elif len(password) < 6:
        flash('密码至少6位', 'error')
    else:
        user = User(username=username, real_name=real_name, phone=phone,
                    role=role, is_admin=(role == 'admin'))
        user.set_password(password)
        db.session.add(user)
        db.session.commit()
        flash('成员添加成功', 'success')
    return redirect(url_for('admin.users'))


@admin_bp.route('/users/toggle/<int:user_id>', methods=['POST'])
@login_required
@admin_required
def toggle_user(user_id):
    user = User.query.get_or_404(user_id)
    if user.username == 'admin':
        flash('不能禁用超级管理员', 'error')
    else:
        user.is_active = not user.is_active
        db.session.commit()
        flash(f'用户已{"启用" if user.is_active else "禁用"}', 'success')
    return redirect(url_for('admin.users'))


@admin_bp.route('/users/reset_password/<int:user_id>', methods=['POST'])
@login_required
@admin_required
def reset_password(user_id):
    user = User.query.get_or_404(user_id)
    new_password = request.form.get('new_password', '')
    if len(new_password) < 6:
        flash('密码至少6位', 'error')
    else:
        user.set_password(new_password)
        db.session.commit()
        flash(f'密码已重置', 'success')
    return redirect(url_for('admin.users'))


@admin_bp.route('/users/edit/<int:user_id>', methods=['POST'])
@login_required
@admin_required
def edit_user(user_id):
    user = User.query.get_or_404(user_id)
    user.real_name = request.form.get('real_name', user.real_name)
    user.phone = request.form.get('phone', user.phone)
    role = request.form.get('role', user.role)
    user.role = role
    user.is_admin = (role == 'admin')
    db.session.commit()
    flash('成员信息已更新', 'success')
    return redirect(url_for('admin.users'))
