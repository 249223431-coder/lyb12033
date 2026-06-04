import os
import uuid
from flask import Blueprint, render_template, request, redirect, url_for, flash
from flask_login import login_required, current_user
from models import db, InspectionItem, InspectionRecord, AnomalyReport
from config import Config
from datetime import datetime, date
from blueprints.auth import admin_required

inspection_bp = Blueprint('inspection', __name__)


@inspection_bp.route('/')
@login_required
def index():
    today = date.today()
    period = request.args.get('period', 'daily')

    period_map = {'daily': '每日', 'biweekly': '每半月', 'weekly': '每周', 'monthly': '每月'}
    items = InspectionItem.query.filter_by(is_active=True, period_type=period).order_by(InspectionItem.sort_order).all()

    today_records = InspectionRecord.query.filter_by(
        user_id=current_user.id,
        check_date=today
    ).all()
    done_item_ids = {r.item_id for r in today_records}

    return render_template('inspection/index.html',
                           items=items,
                           done_item_ids=done_item_ids,
                           today=today,
                           period=period,
                           period_name=period_map.get(period, ''))


@inspection_bp.route('/check/<int:item_id>', methods=['POST'])
@login_required
def check(item_id):
    item = InspectionItem.query.get_or_404(item_id)
    check_value = request.form.get('check_value', '')
    is_normal = request.form.get('is_normal', 'true') == 'true'
    remark = request.form.get('remark', '')

    record = InspectionRecord(
        item_id=item_id,
        user_id=current_user.id,
        check_date=date.today(),
        check_value=check_value,
        is_normal=is_normal,
        remark=remark
    )
    db.session.add(record)

    if not is_normal:
        titles = request.form.getlist('anomaly_title')
        levels = request.form.getlist('anomaly_level')
        locations = request.form.getlist('anomaly_location')
        descs = request.form.getlist('anomaly_desc')
        images = request.files.getlist('anomaly_image')

        anomaly_count = 0
        for i in range(len(titles)):
            title = titles[i].strip()
            if not title:
                continue
            level = levels[i] if i < len(levels) else 'normal'
            loc = locations[i].strip() if i < len(locations) else ''
            desc = descs[i].strip() if i < len(descs) else ''

            image_path = ''
            if images and i < len(images) and images[i] and images[i].filename:
                file = images[i]
                allowed = {'jpg', 'jpeg', 'png', 'gif', 'bmp'}
                ext = file.filename.rsplit('.', 1)[1].lower() if '.' in file.filename else ''
                if ext in allowed:
                    unique_name = f"anomaly_{uuid.uuid4().hex}.{ext}"
                    file.save(os.path.join(Config.UPLOAD_FOLDER, unique_name))
                    image_path = unique_name

            report = AnomalyReport(
                user_id=current_user.id,
                level=level,
                title=title,
                location=loc,
                description=desc,
                image_path=image_path
            )
            db.session.add(report)
            anomaly_count += 1

    db.session.commit()
    if not is_normal and anomaly_count > 0:
        flash(f'点检记录已保存，{anomaly_count}条异常已提报', 'success')
    else:
        flash('点检记录已保存', 'success')
    return redirect(url_for('inspection.index', period=item.period_type))


@inspection_bp.route('/records')
@login_required
def records():
    query = InspectionRecord.query.order_by(InspectionRecord.created_at.desc())
    if not current_user.is_admin:
        query = query.filter_by(user_id=current_user.id)
    records = query.limit(200).all()
    return render_template('inspection/records.html', records=records)


@inspection_bp.route('/items', methods=['GET', 'POST'])
@login_required
@admin_required
def manage_items():
    if request.method == 'POST':
        name = request.form.get('name', '').strip()
        period_type = request.form.get('period_type', 'daily')

        if name:
            item = InspectionItem(
                name=name,
                period_type=period_type,
                sort_order=InspectionItem.query.count()
            )
            db.session.add(item)
            db.session.commit()
            flash('点检项目添加成功', 'success')
        return redirect(url_for('inspection.manage_items'))

    items = InspectionItem.query.order_by(InspectionItem.period_type, InspectionItem.sort_order).all()
    return render_template('inspection/items.html', items=items)


@inspection_bp.route('/items/toggle/<int:item_id>', methods=['POST'])
@login_required
@admin_required
def toggle_item(item_id):
    item = InspectionItem.query.get_or_404(item_id)
    item.is_active = not item.is_active
    db.session.commit()
    flash(f'项目已{"启用" if item.is_active else "停用"}', 'success')
    return redirect(url_for('inspection.manage_items'))


@inspection_bp.route('/items/delete/<int:item_id>', methods=['POST'])
@login_required
@admin_required
def delete_item(item_id):
    item = InspectionItem.query.get_or_404(item_id)
    db.session.delete(item)
    db.session.commit()
    flash('点检项目已删除', 'success')
    return redirect(url_for('inspection.manage_items'))


@inspection_bp.route('/anomaly', methods=['GET', 'POST'])
@login_required
def anomaly():
    if request.method == 'POST':
        level = request.form.get('level', 'normal')
        title = request.form.get('title', '').strip()
        location = request.form.get('location', '').strip()
        description = request.form.get('description', '').strip()

        if not title:
            flash('请输入异常标题', 'error')
            return redirect(url_for('inspection.anomaly'))

        image_path = ''
        if 'image' in request.files:
            file = request.files['image']
            if file and file.filename:
                allowed = {'jpg', 'jpeg', 'png', 'gif', 'bmp'}
                ext = file.filename.rsplit('.', 1)[1].lower() if '.' in file.filename else ''
                if ext in allowed:
                    unique_name = f"anomaly_{uuid.uuid4().hex}.{ext}"
                    file.save(os.path.join(Config.UPLOAD_FOLDER, unique_name))
                    image_path = unique_name

        report = AnomalyReport(
            user_id=current_user.id,
            level=level,
            title=title,
            location=location,
            description=description,
            image_path=image_path
        )
        db.session.add(report)
        db.session.commit()
        flash('异常已提报', 'success')
        return redirect(url_for('inspection.anomaly_list'))

    return render_template('inspection/anomaly_form.html')


@inspection_bp.route('/anomaly/list')
@login_required
def anomaly_list():
    level = request.args.get('level', '')
    status = request.args.get('status', '')

    query = AnomalyReport.query.order_by(
        AnomalyReport.level == 'urgent',
        AnomalyReport.created_at.desc()
    )
    if not current_user.is_admin:
        query = query.filter_by(user_id=current_user.id)
    if level:
        query = query.filter_by(level=level)
    if status:
        query = query.filter_by(status=status)

    reports = query.all()
    return render_template('inspection/anomaly_list.html', reports=reports)


@inspection_bp.route('/anomaly/handle/<int:report_id>', methods=['POST'])
@login_required
def handle_anomaly(report_id):
    report = AnomalyReport.query.get_or_404(report_id)
    handle_action = request.form.get('handle_action', '')
    handler_note = request.form.get('handler_note', '')
    report.handler_note = handler_note
    report.handle_action = handle_action

    if handle_action == 'immediate':
        report.status = request.form.get('status', 'processing')
    else:
        report.status = 'open'

    if 'handle_media' in request.files:
        file = request.files['handle_media']
        if file and file.filename:
            ext = file.filename.rsplit('.', 1)[1].lower() if '.' in file.filename else ''
            if ext in {'jpg', 'jpeg', 'png', 'gif', 'bmp', 'mp4', 'avi', 'mov', 'mp3', 'wav'}:
                unique_name = f"handle_{uuid.uuid4().hex}.{ext}"
                file.save(os.path.join(Config.UPLOAD_FOLDER, unique_name))
                report.handle_media = unique_name

    db.session.commit()
    flash('异常处理已更新', 'success')
    return redirect(url_for('inspection.anomaly_list'))
