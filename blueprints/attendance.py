from flask import Blueprint, render_template, request, redirect, url_for, flash
from flask_login import login_required, current_user
from models import db, AttendanceRecord
from datetime import datetime, date
from blueprints.auth import admin_required

attendance_bp = Blueprint('attendance', __name__)


@attendance_bp.route('/')
@login_required
def index():
    year = request.args.get('year', datetime.now().year, type=int)
    month = request.args.get('month', datetime.now().month, type=int)
    query = AttendanceRecord.query.order_by(AttendanceRecord.created_at.desc())
    if not current_user.is_admin:
        query = query.filter_by(user_id=current_user.id)
    records = query.all()
    return render_template('attendance/index.html', records=records, year=year, month=month)


@attendance_bp.route('/add', methods=['POST'])
@login_required
def add():
    record_type = request.form.get('record_type', 'leave')
    start_date = request.form.get('start_date', '')
    end_date = request.form.get('end_date', '')
    start_time = request.form.get('start_time', '')
    end_time = request.form.get('end_time', '')
    reason = request.form.get('reason', '')

    if not start_date:
        flash('请选择开始日期', 'error')
        return redirect(url_for('attendance.index'))

    record = AttendanceRecord(
        user_id=current_user.id,
        record_type=record_type,
        start_date=datetime.strptime(start_date, '%Y-%m-%d').date(),
        end_date=datetime.strptime(end_date, '%Y-%m-%d').date() if end_date else None,
        start_time=start_time,
        end_time=end_time,
        reason=reason
    )

    if record_type == 'leave':
        if end_date and end_date != start_date:
            delta = datetime.strptime(end_date, '%Y-%m-%d').date() - datetime.strptime(start_date, '%Y-%m-%d').date()
            record.duration_days = delta.days + 1
        else:
            record.duration_days = 1
    elif record_type == 'overtime':
        record.duration_days = 0.5

    record.status = 'recorded'
    db.session.add(record)
    db.session.commit()
    flash('记录添加成功', 'success')
    return redirect(url_for('attendance.index'))


@attendance_bp.route('/delete/<int:record_id>', methods=['POST'])
@login_required
def delete(record_id):
    record = AttendanceRecord.query.get_or_404(record_id)
    if not current_user.is_admin and record.user_id != current_user.id:
        flash('无权操作', 'error')
        return redirect(url_for('attendance.index'))
    record.status = 'cancelled'
    db.session.commit()
    flash('记录已取消', 'success')
    return redirect(url_for('attendance.index'))
