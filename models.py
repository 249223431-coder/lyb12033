from flask_sqlalchemy import SQLAlchemy
from flask_login import UserMixin
from werkzeug.security import generate_password_hash, check_password_hash
from datetime import datetime, timedelta

def china_now():
    return datetime.utcnow() + timedelta(hours=8)


db = SQLAlchemy()


class User(UserMixin, db.Model):
    __tablename__ = 'users'
    id = db.Column(db.Integer, primary_key=True)
    username = db.Column(db.String(80), unique=True, nullable=False, index=True)
    password_hash = db.Column(db.String(256), nullable=False)
    real_name = db.Column(db.String(80), nullable=False)
    phone = db.Column(db.String(20), nullable=False)
    role = db.Column(db.String(20), nullable=False, default='member')  # admin / member
    is_active = db.Column(db.Boolean, default=True)
    is_admin = db.Column(db.Boolean, default=False)
    created_at = db.Column(db.DateTime, default=china_now)
    avatar_url = db.Column(db.String(500), default='')

    attendance_records = db.relationship('AttendanceRecord', backref='user', lazy='dynamic')
    anomaly_reports = db.relationship('AnomalyReport', backref='user', lazy='dynamic')
    inspection_records = db.relationship('InspectionRecord', backref='user', lazy='dynamic')
    stock_transactions = db.relationship('StockTransaction', backref='user', lazy='dynamic')

    def set_password(self, password):
        self.password_hash = generate_password_hash(password)

    def check_password(self, password):
        return check_password_hash(self.password_hash, password)

    def to_dict(self):
        return {
            'id': self.id,
            'username': self.username,
            'real_name': self.real_name,
            'phone': self.phone,
            'role': self.role,
            'is_active': self.is_active,
            'is_admin': self.is_admin,
            'created_at': self.created_at.strftime('%Y-%m-%d %H:%M') if self.created_at else '',
        }


class Motor(db.Model):
    __tablename__ = 'motors'
    id = db.Column(db.Integer, primary_key=True)
    motor_code = db.Column(db.String(100), default='', index=True)
    name = db.Column(db.String(300), nullable=False)
    room = db.Column(db.String(100), default='')
    cabinet = db.Column(db.String(100), default='')
    voltage = db.Column(db.String(50), default='')
    power_kw = db.Column(db.String(50), default='')
    current_a = db.Column(db.String(50), default='')
    power_factor = db.Column(db.String(50), default='')
    speed = db.Column(db.String(50), default='')
    motor_model = db.Column(db.String(200), default='')
    location = db.Column(db.String(200), default='')
    position = db.Column(db.String(200), default='')
    front_bearing = db.Column(db.String(100), default='')
    rear_bearing = db.Column(db.String(100), default='')
    bus_node = db.Column(db.String(100), default='')

    def to_dict(self):
        return {
            'id': self.id, 'motor_code': self.motor_code, 'name': self.name,
            'room': self.room, 'cabinet': self.cabinet, 'voltage': self.voltage,
            'power_kw': self.power_kw, 'current_a': self.current_a,
            'power_factor': self.power_factor, 'speed': self.speed,
            'motor_model': self.motor_model, 'location': self.location,
            'position': self.position, 'front_bearing': self.front_bearing,
            'rear_bearing': self.rear_bearing, 'bus_node': self.bus_node,
        }


class MotorRepair(db.Model):
    __tablename__ = 'motor_repairs'
    id = db.Column(db.Integer, primary_key=True)
    user_id = db.Column(db.Integer, db.ForeignKey('users.id'), nullable=False)
    repair_name = db.Column(db.String(300), nullable=False)
    use_location = db.Column(db.String(200), default='')
    pole_count = db.Column(db.String(50), default='')
    power_spec = db.Column(db.String(100), default='')
    model_spec = db.Column(db.String(200), default='')
    fault_desc = db.Column(db.Text, default='')
    sap_price = db.Column(db.String(100), default='')
    repair_quote = db.Column(db.String(100), default='')
    vendor = db.Column(db.String(200), default='')
    repair_number = db.Column(db.String(100), default='')
    aplus_notice = db.Column(db.String(100), default='')
    aplus_budget = db.Column(db.String(100), default='')
    cost_center = db.Column(db.String(100), default='')
    opex_no = db.Column(db.String(100), default='')
    opex_line = db.Column(db.String(100), default='')
    acceptance_result = db.Column(db.Text, default='')
    approver_gm = db.Column(db.String(50), default='')
    approver_director = db.Column(db.String(50), default='')
    approver_manager = db.Column(db.String(50), default='')
    approver_chief = db.Column(db.String(50), default='')
    approver_section_chief = db.Column(db.String(50), default='')
    approver_handler = db.Column(db.String(50), default='')
    acceptor_chief = db.Column(db.String(50), default='')
    acceptor_handler = db.Column(db.String(50), default='')
    status = db.Column(db.String(20), default='draft')
    created_at = db.Column(db.DateTime, default=china_now)
    updated_at = db.Column(db.DateTime, default=china_now, onupdate=china_now)

    user = db.relationship('User', backref='motor_repairs')

    def to_dict(self):
        return {
            'id': self.id, 'repair_name': self.repair_name,
            'use_location': self.use_location, 'pole_count': self.pole_count,
            'power_spec': self.power_spec, 'model_spec': self.model_spec,
            'fault_desc': self.fault_desc, 'sap_price': self.sap_price,
            'repair_quote': self.repair_quote, 'vendor': self.vendor,
            'repair_number': self.repair_number, 'aplus_notice': self.aplus_notice,
            'aplus_budget': self.aplus_budget, 'cost_center': self.cost_center,
            'opex_no': self.opex_no, 'opex_line': self.opex_line,
            'acceptance_result': self.acceptance_result,
            'status': self.status,
            'created_at': self.created_at.strftime('%Y-%m-%d %H:%M') if self.created_at else '',
        }


class AttendanceRecord(db.Model):
    __tablename__ = 'attendance_records'
    id = db.Column(db.Integer, primary_key=True)
    user_id = db.Column(db.Integer, db.ForeignKey('users.id'), nullable=False)
    record_type = db.Column(db.String(20), nullable=False)  # leave / overtime
    start_date = db.Column(db.Date, nullable=False)
    end_date = db.Column(db.Date, nullable=True)
    start_time = db.Column(db.String(10), default='')
    end_time = db.Column(db.String(10), default='')
    duration_days = db.Column(db.Float, default=0)
    reason = db.Column(db.Text, default='')
    status = db.Column(db.String(20), default='recorded')  # recorded / cancelled
    created_at = db.Column(db.DateTime, default=china_now)
    updated_at = db.Column(db.DateTime, default=china_now, onupdate=china_now)

    def to_dict(self):
        return {
            'id': self.id,
            'user_id': self.user_id,
            'user_name': self.user.real_name if self.user else '',
            'record_type': self.record_type,
            'start_date': self.start_date.strftime('%Y-%m-%d') if self.start_date else '',
            'end_date': self.end_date.strftime('%Y-%m-%d') if self.end_date else '',
            'start_time': self.start_time,
            'end_time': self.end_time,
            'duration_days': self.duration_days,
            'reason': self.reason,
            'status': self.status,
            'created_at': self.created_at.strftime('%Y-%m-%d %H:%M') if self.created_at else '',
        }


class DocumentCategory(db.Model):
    __tablename__ = 'document_categories'
    id = db.Column(db.Integer, primary_key=True)
    name = db.Column(db.String(100), nullable=False, unique=True)
    description = db.Column(db.Text, default='')
    sort_order = db.Column(db.Integer, default=0)
    created_at = db.Column(db.DateTime, default=china_now)

    documents = db.relationship('Document', backref='category', lazy='dynamic')

    def to_dict(self):
        return {
            'id': self.id,
            'name': self.name,
            'description': self.description,
            'sort_order': self.sort_order,
            'doc_count': self.documents.count(),
        }


class Document(db.Model):
    __tablename__ = 'documents'
    id = db.Column(db.Integer, primary_key=True)
    category_id = db.Column(db.Integer, db.ForeignKey('document_categories.id'), nullable=False)
    title = db.Column(db.String(200), nullable=False)
    filename = db.Column(db.String(500), nullable=False)
    original_filename = db.Column(db.String(500), nullable=False)
    file_size = db.Column(db.Integer, default=0)
    file_type = db.Column(db.String(50), default='')
    uploader_id = db.Column(db.Integer, db.ForeignKey('users.id'), nullable=False)
    description = db.Column(db.Text, default='')
    download_count = db.Column(db.Integer, default=0)
    created_at = db.Column(db.DateTime, default=china_now)

    uploader = db.relationship('User', backref='documents')

    def to_dict(self):
        return {
            'id': self.id,
            'category_id': self.category_id,
            'category_name': self.category.name if self.category else '',
            'title': self.title,
            'filename': self.filename,
            'original_filename': self.original_filename,
            'file_size': self.file_size,
            'file_type': self.file_type,
            'uploader_name': self.uploader.real_name if self.uploader else '',
            'description': self.description,
            'download_count': self.download_count,
            'created_at': self.created_at.strftime('%Y-%m-%d %H:%M') if self.created_at else '',
        }

    def size_display(self):
        size = self.file_size
        if size < 1024:
            return f'{size}B'
        elif size < 1024 * 1024:
            return f'{size / 1024:.1f}KB'
        elif size < 1024 * 1024 * 1024:
            return f'{size / (1024 * 1024):.1f}MB'
        return f'{size / (1024 * 1024 * 1024):.1f}GB'


class InspectionItem(db.Model):
    __tablename__ = 'inspection_items'
    id = db.Column(db.Integer, primary_key=True)
    name = db.Column(db.String(200), nullable=False)
    location = db.Column(db.String(200), default='')
    check_method = db.Column(db.Text, default='')
    normal_range = db.Column(db.String(200), default='')
    unit = db.Column(db.String(50), default='')
    period_type = db.Column(db.String(20), default='daily')  # daily / weekly / monthly
    is_active = db.Column(db.Boolean, default=True)
    sort_order = db.Column(db.Integer, default=0)
    created_at = db.Column(db.DateTime, default=china_now)

    inspection_records = db.relationship('InspectionRecord', backref='item', lazy='dynamic')

    def to_dict(self):
        return {
            'id': self.id,
            'name': self.name,
            'location': self.location,
            'check_method': self.check_method,
            'normal_range': self.normal_range,
            'unit': self.unit,
            'period_type': self.period_type,
            'is_active': self.is_active,
            'sort_order': self.sort_order,
        }


class InspectionRecord(db.Model):
    __tablename__ = 'inspection_records'
    id = db.Column(db.Integer, primary_key=True)
    item_id = db.Column(db.Integer, db.ForeignKey('inspection_items.id'), nullable=False)
    user_id = db.Column(db.Integer, db.ForeignKey('users.id'), nullable=False)
    check_date = db.Column(db.Date, nullable=False)
    check_value = db.Column(db.String(100), default='')
    is_normal = db.Column(db.Boolean, default=True)
    remark = db.Column(db.Text, default='')
    created_at = db.Column(db.DateTime, default=china_now)

    def to_dict(self):
        return {
            'id': self.id,
            'item_id': self.item_id,
            'item_name': self.item.name if self.item else '',
            'user_id': self.user_id,
            'user_name': self.user.real_name if self.user else '',
            'check_date': self.check_date.strftime('%Y-%m-%d') if self.check_date else '',
            'check_value': self.check_value,
            'is_normal': self.is_normal,
            'remark': self.remark,
            'created_at': self.created_at.strftime('%Y-%m-%d %H:%M') if self.created_at else '',
        }


class AnomalyReport(db.Model):
    __tablename__ = 'anomaly_reports'
    id = db.Column(db.Integer, primary_key=True)
    user_id = db.Column(db.Integer, db.ForeignKey('users.id'), nullable=False)
    level = db.Column(db.String(20), nullable=False)  # normal / urgent
    title = db.Column(db.String(200), nullable=False)
    location = db.Column(db.String(200), default='')
    description = db.Column(db.Text, default='')
    image_path = db.Column(db.String(500), default='')
    status = db.Column(db.String(20), default='open')  # open / processing / resolved
    handle_action = db.Column(db.String(20), default='')  # immediate / observe / shutdown
    handler_note = db.Column(db.Text, default='')
    handle_media = db.Column(db.String(500), default='')
    created_at = db.Column(db.DateTime, default=china_now)
    updated_at = db.Column(db.DateTime, default=china_now, onupdate=china_now)

    def to_dict(self):
        return {
            'id': self.id,
            'user_id': self.user_id,
            'user_name': self.user.real_name if self.user else '',
            'level': self.level,
            'title': self.title,
            'location': self.location,
            'description': self.description,
            'image_path': self.image_path,
            'status': self.status,
            'handle_action': self.handle_action,
            'handler_note': self.handler_note,
            'handle_media': self.handle_media,
            'created_at': self.created_at.strftime('%Y-%m-%d %H:%M') if self.created_at else '',
        }


class Notification(db.Model):
    __tablename__ = 'notifications'
    id = db.Column(db.Integer, primary_key=True)
    user_id = db.Column(db.Integer, db.ForeignKey('users.id'), nullable=False)
    title = db.Column(db.String(200), nullable=False)
    content = db.Column(db.Text, default='')
    is_read = db.Column(db.Boolean, default=False)
    created_at = db.Column(db.DateTime, default=china_now)

    sender = db.relationship('User', backref='notifications', foreign_keys=[user_id])

    def to_dict(self):
        return {
            'id': self.id,
            'title': self.title,
            'content': self.content,
            'is_read': self.is_read,
            'created_at': self.created_at.strftime('%Y-%m-%d %H:%M') if self.created_at else '',
        }


class SparePart(db.Model):
    __tablename__ = 'spare_parts'
    id = db.Column(db.Integer, primary_key=True)
    part_code = db.Column(db.String(100), unique=True, nullable=False, index=True)
    name = db.Column(db.String(200), nullable=False)
    model_spec = db.Column(db.String(200), default='')
    unit = db.Column(db.String(20), default='个')
    category = db.Column(db.String(100), default='')
    location = db.Column(db.String(200), default='')
    stock_quantity = db.Column(db.Integer, default=0)
    safety_stock = db.Column(db.Integer, default=0)
    warning_stock = db.Column(db.Integer, default=0)
    price = db.Column(db.Float, default=0.0)
    supplier = db.Column(db.String(200), default='')
    remark = db.Column(db.Text, default='')
    is_active = db.Column(db.Boolean, default=True)
    created_at = db.Column(db.DateTime, default=china_now)
    updated_at = db.Column(db.DateTime, default=china_now, onupdate=china_now)

    transactions = db.relationship('StockTransaction', backref='part', lazy='dynamic')

    def to_dict(self):
        return {
            'id': self.id,
            'part_code': self.part_code,
            'name': self.name,
            'model_spec': self.model_spec,
            'unit': self.unit,
            'category': self.category,
            'location': self.location,
            'stock_quantity': self.stock_quantity,
            'safety_stock': self.safety_stock,
            'warning_stock': self.warning_stock,
            'price': self.price,
            'supplier': self.supplier,
            'remark': self.remark,
            'is_active': self.is_active,
        }


class StockTransaction(db.Model):
    __tablename__ = 'stock_transactions'
    id = db.Column(db.Integer, primary_key=True)
    part_id = db.Column(db.Integer, db.ForeignKey('spare_parts.id'), nullable=False)
    user_id = db.Column(db.Integer, db.ForeignKey('users.id'), nullable=False)
    trans_type = db.Column(db.String(20), nullable=False)  # in / out
    quantity = db.Column(db.Integer, nullable=False)
    operator_name = db.Column(db.String(80), default='')
    purpose = db.Column(db.String(200), default='')
    remark = db.Column(db.Text, default='')
    created_at = db.Column(db.DateTime, default=china_now)

    def to_dict(self):
        return {
            'id': self.id,
            'part_id': self.part_id,
            'part_name': self.part.name if self.part else '',
            'part_code': self.part.part_code if self.part else '',
            'user_id': self.user_id,
            'user_name': self.user.real_name if self.user else '',
            'trans_type': self.trans_type,
            'quantity': self.quantity,
            'operator_name': self.operator_name,
            'purpose': self.purpose,
            'remark': self.remark,
            'created_at': self.created_at.strftime('%Y-%m-%d %H:%M') if self.created_at else '',
        }


class PlcData(db.Model):
    __tablename__ = 'plc_data'
    id = db.Column(db.Integer, primary_key=True)
    tag_name = db.Column(db.String(50), nullable=False)
    value = db.Column(db.Float, default=0.0)
    unit = db.Column(db.String(20), default='')
    updated_at = db.Column(db.DateTime, default=china_now)

    def to_dict(self):
        return {
            'tag_name': self.tag_name,
            'value': self.value,
            'unit': self.unit,
            'updated_at': self.updated_at.strftime('%Y-%m-%d %H:%M:%S') if self.updated_at else '',
        }


class CollectionForm(db.Model):
    __tablename__ = 'collection_forms'
    id = db.Column(db.Integer, primary_key=True)
    title = db.Column(db.String(200), nullable=False)
    description = db.Column(db.Text, default='')
    icon = db.Column(db.String(50), default='bi-clipboard')
    is_active = db.Column(db.Boolean, default=True)
    sort_order = db.Column(db.Integer, default=0)
    created_by = db.Column(db.Integer, db.ForeignKey('users.id'), nullable=False)
    created_at = db.Column(db.DateTime, default=china_now)

    creator = db.relationship('User', backref='collection_forms')
    fields = db.relationship('CollectionField', backref='form', lazy='dynamic',
                             order_by='CollectionField.sort_order')
    submissions = db.relationship('CollectionSubmission', backref='form', lazy='dynamic')

    def to_dict(self):
        return {
            'id': self.id,
            'title': self.title,
            'description': self.description,
            'icon': self.icon,
            'is_active': self.is_active,
            'sort_order': self.sort_order,
            'created_at': self.created_at.strftime('%Y-%m-%d %H:%M') if self.created_at else '',
        }


class CollectionField(db.Model):
    __tablename__ = 'collection_fields'
    id = db.Column(db.Integer, primary_key=True)
    form_id = db.Column(db.Integer, db.ForeignKey('collection_forms.id'), nullable=False)
    label = db.Column(db.String(200), nullable=False)
    field_type = db.Column(db.String(20), nullable=False, default='text')
    options = db.Column(db.Text, default='')
    placeholder = db.Column(db.String(200), default='')
    required = db.Column(db.Boolean, default=False)
    sort_order = db.Column(db.Integer, default=0)

    values = db.relationship('CollectionValue', backref='field', lazy='dynamic')

    def get_options(self):
        if self.options:
            return [o.strip() for o in self.options.split('\n') if o.strip()]
        return []

    def to_dict(self):
        return {
            'id': self.id,
            'form_id': self.form_id,
            'label': self.label,
            'field_type': self.field_type,
            'options': self.get_options(),
            'placeholder': self.placeholder,
            'required': self.required,
            'sort_order': self.sort_order,
        }


class CollectionSubmission(db.Model):
    __tablename__ = 'collection_submissions'
    id = db.Column(db.Integer, primary_key=True)
    form_id = db.Column(db.Integer, db.ForeignKey('collection_forms.id'), nullable=False)
    user_id = db.Column(db.Integer, db.ForeignKey('users.id'), nullable=False)
    created_at = db.Column(db.DateTime, default=china_now)

    user = db.relationship('User', backref='collection_submissions')
    values = db.relationship('CollectionValue', backref='submission', lazy='dynamic')

    def to_dict(self):
        return {
            'id': self.id,
            'form_id': self.form_id,
            'user_name': self.user.real_name if self.user else '',
            'created_at': self.created_at.strftime('%Y-%m-%d %H:%M') if self.created_at else '',
        }


class CollectionValue(db.Model):
    __tablename__ = 'collection_values'
    id = db.Column(db.Integer, primary_key=True)
    submission_id = db.Column(db.Integer, db.ForeignKey('collection_submissions.id'), nullable=False)
    field_id = db.Column(db.Integer, db.ForeignKey('collection_fields.id'), nullable=False)
    value = db.Column(db.Text, default='')

    def to_dict(self):
        return {
            'id': self.id,
            'submission_id': self.submission_id,
            'field_id': self.field_id,
            'value': self.value,
        }





class ShutdownPlan(db.Model):
    __tablename__ = 'shutdown_plans'
    id = db.Column(db.Integer, primary_key=True)
    sheet_name = db.Column(db.String(50), nullable=False)
    plan_date = db.Column(db.Date, nullable=False, index=True)
    start_time = db.Column(db.String(10), default='')
    end_time = db.Column(db.String(10), default='')
    department = db.Column(db.String(200), default='')
    status = db.Column(db.String(20), default='loaded')  # loaded / notified / completed
    created_at = db.Column(db.DateTime, default=china_now)

    items = db.relationship('ShutdownPlanItem', backref='plan', lazy='dynamic',
                            order_by='ShutdownPlanItem.seq')


class ShutdownPlanItem(db.Model):
    __tablename__ = 'shutdown_plan_items'
    id = db.Column(db.Integer, primary_key=True)
    plan_id = db.Column(db.Integer, db.ForeignKey('shutdown_plans.id'), nullable=False)
    seq = db.Column(db.Integer, default=0)
    project_name = db.Column(db.Text, default='')
    category = db.Column(db.String(20), default='')
    responsible = db.Column(db.String(100), default='')
    responsible_user_id = db.Column(db.Integer, nullable=True)
    members = db.Column(db.String(200), default='')
    safety_notes = db.Column(db.Text, default='')
    completed = db.Column(db.Boolean, default=False)
    complete_note = db.Column(db.Text, default='')
    confirmed_by = db.Column(db.Integer, nullable=True)
    confirmed_at = db.Column(db.DateTime, nullable=True)


class OvertimePreReport(db.Model):
    __tablename__ = 'overtime_pre_reports'
    id = db.Column(db.Integer, primary_key=True)
    user_id = db.Column(db.Integer, db.ForeignKey('users.id'), nullable=False)
    holiday_date = db.Column(db.Date, nullable=False, index=True)
    holiday_type = db.Column(db.String(20), nullable=False)  # weekend / holiday
    holiday_name = db.Column(db.String(100), default='')
    start_time = db.Column(db.String(10), default='')
    end_time = db.Column(db.String(10), default='')
    hours = db.Column(db.Float, default=0)
    reason = db.Column(db.Text, default='')
    status = db.Column(db.String(20), default='submitted')  # submitted / cancelled / exported
    created_at = db.Column(db.DateTime, default=china_now)
    updated_at = db.Column(db.DateTime, default=china_now, onupdate=china_now)

    user = db.relationship('User', backref='overtime_pre_reports')

    def to_dict(self):
        return {
            'id': self.id,
            'user_name': self.user.real_name if self.user else '',
            'holiday_date': self.holiday_date.strftime('%Y-%m-%d') if self.holiday_date else '',
            'holiday_type': self.holiday_type,
            'holiday_name': self.holiday_name,
            'start_time': self.start_time,
            'end_time': self.end_time,
            'hours': self.hours,
            'reason': self.reason,
            'status': self.status,
            'created_at': self.created_at.strftime('%Y-%m-%d %H:%M') if self.created_at else '',
        }


class PurchaseRequisition(db.Model):
    __tablename__ = 'purchase_requisitions'
    id = db.Column(db.Integer, primary_key=True)
    user_id = db.Column(db.Integer, db.ForeignKey('users.id'), nullable=False)
    part_name = db.Column(db.String(200), nullable=False)
    part_code = db.Column(db.String(100), default='')
    model_spec = db.Column(db.String(200), default='')
    quantity = db.Column(db.Integer, default=1)
    purpose = db.Column(db.Text, default='')
    image_path = db.Column(db.String(500), default='')
    status = db.Column(db.String(20), default='pending')
    created_at = db.Column(db.DateTime, default=china_now)

    user = db.relationship('User', backref='purchase_requisitions')

    def to_dict(self):
        return {
            'id': self.id,
            'user_name': self.user.real_name if self.user else '',
            'part_name': self.part_name,
            'part_code': self.part_code or '',
            'model_spec': self.model_spec,
            'quantity': self.quantity,
            'purpose': self.purpose,
            'image_path': self.image_path,
            'status': self.status,
            'created_at': self.created_at.strftime('%Y-%m-%d %H:%M') if self.created_at else '',
        }
