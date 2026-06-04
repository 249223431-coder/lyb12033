from flask import Blueprint, render_template, request, redirect, url_for, flash, jsonify
from flask_login import login_required, current_user
from models import db, Notification, User
from blueprints.auth import admin_required

notifications_bp = Blueprint('notifications', __name__)


@notifications_bp.route('/')
@login_required
def index():
    if current_user.is_admin:
        notifications = Notification.query.order_by(Notification.created_at.desc()).all()
    else:
        notifications = Notification.query.filter_by(user_id=current_user.id).order_by(Notification.created_at.desc()).all()
    return render_template('notifications/index.html', notifications=notifications)


@notifications_bp.route('/send', methods=['GET', 'POST'])
@login_required
def send():
    if request.method == 'POST':
        title = request.form.get('title', '').strip()
        content = request.form.get('content', '').strip()
        send_to = request.form.get('send_to', 'all')

        if not title:
            flash('请输入通知标题', 'error')
            return redirect(url_for('notifications.send'))

        if send_to == 'all':
            users = User.query.filter_by(is_active=True).all()
        else:
            users = [current_user]

        count = 0
        for user in users:
            notification = Notification(
                user_id=user.id,
                title=title,
                content=content
            )
            db.session.add(notification)
            count += 1

        db.session.commit()
        flash(f'通知已发送给 {count} 位成员', 'success')
        return redirect(url_for('notifications.index'))

    users = User.query.filter_by(is_active=True).all()
    return render_template('notifications/send.html', users=users)


@notifications_bp.route('/delete/<int:notif_id>', methods=['POST'])
@login_required
@admin_required
def delete(notif_id):
    notification = Notification.query.get_or_404(notif_id)
    db.session.delete(notification)
    db.session.commit()
    flash('通知已删除', 'success')
    return redirect(url_for('notifications.index'))


@notifications_bp.route('/detail/<int:notification_id>')
@login_required
def detail(notification_id):
    notification = Notification.query.get_or_404(notification_id)
    if notification.user_id != current_user.id and not current_user.is_admin:
        flash('无权查看', 'error')
        return redirect(url_for('notifications.index'))
    notification.is_read = True
    db.session.commit()
    return render_template('notifications/detail.html', notification=notification)


@notifications_bp.route('/read_all', methods=['POST'])
@login_required
def read_all():
    Notification.query.filter_by(user_id=current_user.id, is_read=False).update({'is_read': True})
    db.session.commit()
    flash('全部标记为已读', 'success')
    return redirect(url_for('notifications.index'))


@notifications_bp.route('/unread_count')
@login_required
def unread_count():
    count = Notification.query.filter_by(user_id=current_user.id, is_read=False).count()
    return jsonify({'count': count})
