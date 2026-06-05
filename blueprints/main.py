from flask import Blueprint, render_template, redirect, url_for, jsonify
from flask_login import login_required, current_user
from models import db, Notification, User, InspectionItem, InspectionRecord, AnomalyReport, SparePart, Document, AttendanceRecord, PurchaseRequisition, StockTransaction, MotorRepair
from datetime import date, datetime, timedelta

main_bp = Blueprint('main', __name__)


@main_bp.route('/')
@login_required
def index():
    today = date.today()
    unread_count = Notification.query.filter_by(is_read=False).count()
    greeting = _get_greeting()
    warm_message = _get_warm_message()

    total_members = User.query.filter_by(is_active=True).count()

    absent_users = User.query.filter(User.is_active == True).filter(
        User.id.in_(
            db.session.query(AttendanceRecord.user_id).filter(
                AttendanceRecord.record_type == 'leave',
                AttendanceRecord.status != 'cancelled',
                AttendanceRecord.start_date <= today,
                db.or_(AttendanceRecord.end_date >= today, AttendanceRecord.end_date == None)
            )
        )
    ).all()
    today_absent = len(absent_users)
    absent_names = [u.real_name for u in absent_users]

    inspection_items = InspectionItem.query.filter_by(is_active=True).count()
    today_inspected = InspectionRecord.query.filter_by(check_date=today).count()

    open_anomalies = AnomalyReport.query.filter(
        AnomalyReport.status.in_(['open', 'processing'])
    ).count()

    urgent_anomalies = AnomalyReport.query.filter(
        AnomalyReport.level == 'urgent',
        AnomalyReport.status.in_(['open', 'processing'])
    ).count()

    low_stock_parts_count = SparePart.query.filter(
        SparePart.is_active == True,
        SparePart.stock_quantity <= SparePart.warning_stock
    ).count()

    purch_req_count = PurchaseRequisition.query.filter_by(status='pending').count()
    low_stock_parts = low_stock_parts_count + purch_req_count

    recent_activities = _build_activity_feed(limit=10)

    inspection_rate = round((today_inspected / inspection_items * 100)) if inspection_items > 0 else 0

    return render_template('index.html',
                           unread_count=unread_count,
                           greeting=greeting,
                           warm_message=warm_message,
                           total_members=total_members,
                           today_absent=today_absent,
                           absent_names=absent_names,
                           inspection_items=inspection_items,
                           today_inspected=today_inspected,
                           open_anomalies=open_anomalies,
                           urgent_anomalies=urgent_anomalies,
                           low_stock_parts=low_stock_parts,
                           recent_activities=recent_activities,
                           today_inspection_items=[],
                           inspection_rate=inspection_rate,
                           today=today)


@main_bp.route('/api/absent')
@login_required
def api_absent():
    today = date.today()
    absent_users = User.query.filter(User.is_active == True).filter(
        User.id.in_(
            db.session.query(AttendanceRecord.user_id).filter(
                AttendanceRecord.record_type == 'leave',
                AttendanceRecord.status != 'cancelled',
                AttendanceRecord.start_date <= today,
                db.or_(AttendanceRecord.end_date >= today, AttendanceRecord.end_date == None)
            )
        )
    ).all()
    return jsonify([{
        'name': u.real_name,
        'phone': u.phone,
        'role': '管理员' if u.is_admin else '班组成员'
    } for u in absent_users])


def _get_greeting():
    hour = datetime.now().hour
    if hour < 6:
        return '夜深了'
    elif hour < 9:
        return '早上好'
    elif hour < 12:
        return '上午好'
    elif hour < 14:
        return '中午好'
    elif hour < 18:
        return '下午好'
    else:
        return '晚上好'


def _get_warm_message():
    today = date.today()
    weekday = today.weekday()
    
    warm_messages = [
        "周一好！新的一周开始了，加油！💪",
        "周二继续努力，保持好状态！✨",
        "周三啦，一周过半，坚持就是胜利！💯",
        "周四快乐！明天就是周五啦~ 🎉",
        "周五到了！周末就在眼前，开心！🌈",
        "周六愉快！好好放松一下吧~ 🛝",
        "周日休息，为下周充充电！🔋",
    ]
    
    holiday_messages = {
        '01-01': '🎉 新年快乐！愿新的一年万事如意！',
        '02-14': '💝 情人节快乐！',
        '03-08': '👩 妇女节快乐！',
        '05-01': '🎉 劳动节快乐！',
        '06-01': '👶 儿童节快乐！',
        '07-01': '🇨🇳 建党节快乐！',
        '08-01': '🎖️ 建军节快乐！',
        '09-10': '👨‍🏫 教师节快乐！',
        '10-01': '🇨🇳 国庆节快乐！',
        '12-25': '🎄 圣诞节快乐！',
    }
    
    date_str = today.strftime('%m-%d')
    if date_str in holiday_messages:
        return holiday_messages[date_str]
    
    return warm_messages[weekday]


def _build_activity_feed(limit=10):
    activities = []

    notices = Notification.query.order_by(Notification.created_at.desc()).limit(5).all()
    for n in notices:
        activities.append({
            'type': 'notification',
            'icon': 'bi-bell-fill',
            'icon_color': '#1565c0',
            'icon_bg': '#e3f2fd',
            'title': n.title,
            'desc': (n.content or '')[:40],
            'time': n.created_at,
            'link': url_for('notifications.detail', notification_id=n.id),
            'dot': not n.is_read,
        })

    docs = Document.query.order_by(Document.created_at.desc()).limit(3).all()
    for d in docs:
        activities.append({
            'type': 'document',
            'icon': 'bi-file-earmark-text-fill',
            'icon_color': '#e65100',
            'icon_bg': '#fff3e0',
            'title': d.title,
            'desc': f'{d.size_display()} · {d.uploader.real_name if d.uploader else ""}',
            'time': d.created_at,
            'link': url_for('documents.index'),
            'dot': False,
        })

    records = InspectionRecord.query.order_by(InspectionRecord.created_at.desc()).limit(3).all()
    for r in records:
        activities.append({
            'type': 'inspection',
            'icon': 'bi-clipboard-check',
            'icon_color': '#2e7d32',
            'icon_bg': '#e8f5e9',
            'title': f'点检: {r.item.name if r.item else ""}',
            'desc': f'值: {r.check_value} · {"正常" if r.is_normal else "异常"}',
            'time': r.created_at,
            'link': url_for('inspection.records'),
            'dot': False,
        })

    anomalies = AnomalyReport.query.order_by(AnomalyReport.created_at.desc()).limit(3).all()
    for a in anomalies:
        activities.append({
            'type': 'anomaly',
            'icon': 'bi-exclamation-triangle-fill',
            'icon_color': '#c62828',
            'icon_bg': '#fce4ec',
            'title': f'异常: {a.title}',
            'desc': f'{a.location or ""} · {a.user.real_name if a.user else ""}',
            'time': a.created_at,
            'link': url_for('inspection.anomaly_list'),
            'dot': a.status in ('open', 'processing'),
        })

    purchases = PurchaseRequisition.query.order_by(PurchaseRequisition.created_at.desc()).limit(2).all()
    for p in purchases:
        activities.append({
            'type': 'purchase',
            'icon': 'bi-cart-fill',
            'icon_color': '#7b1fa2',
            'icon_bg': '#f3e5f5',
            'title': f'请购: {p.part_name}',
            'desc': f'{p.model_spec or ""} · {p.quantity}个',
            'time': p.created_at,
            'link': url_for('spare_parts.purchase_list'),
            'dot': p.status == 'pending',
        })

    trans = StockTransaction.query.order_by(StockTransaction.created_at.desc()).limit(3).all()
    for t in trans:
        part_name = t.part.name if t.part else t.part_code
        activities.append({
            'type': 'stock',
            'icon': 'bi-box-arrow-in-down' if t.trans_type == 'in' else 'bi-box-arrow-up',
            'icon_color': '#2e7d32' if t.trans_type == 'in' else '#e65100',
            'icon_bg': '#e8f5e9' if t.trans_type == 'in' else '#fff3e0',
            'title': f"{'入库' if t.trans_type == 'in' else '出库'}: {part_name}",
            'desc': f'{t.quantity}个 · {t.purpose or t.remark or ""}',
            'time': t.created_at,
            'link': url_for('spare_parts.transactions'),
            'dot': False,
        })

    repairs = MotorRepair.query.order_by(MotorRepair.created_at.desc()).limit(3).all()
    for r in repairs:
        activities.append({
            'type': 'motor_repair',
            'icon': 'bi-gear-fill',
            'icon_color': '#e65100',
            'icon_bg': '#fff3e0',
            'title': f'电机维修: {r.repair_name}',
            'desc': f'{r.model_spec or ""} · {r.use_location or ""}',
            'time': r.created_at,
            'link': url_for('motor.repair_list'),
            'dot': r.status == 'draft',
        })

    activities.sort(key=lambda x: x['time'], reverse=True)
    return activities[:limit]
