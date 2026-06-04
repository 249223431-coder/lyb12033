from flask import Blueprint, render_template, request, redirect, url_for, flash, jsonify
from flask_login import login_required, current_user
from models import db, SparePart, StockTransaction, PurchaseRequisition
from blueprints.auth import admin_required
import os, uuid
from config import Config

spare_parts_bp = Blueprint('spare_parts', __name__)


@spare_parts_bp.route('/')
@login_required
def index():
    search = request.args.get('search', '').strip()
    category = request.args.get('category', '').strip()
    stock_status = request.args.get('stock_status', '').strip()

    has_filter = search or category or stock_status
    parts = []
    categories = []
    if has_filter:
        query = SparePart.query.filter_by(is_active=True).order_by(SparePart.name)
        if search:
            query = query.filter(
                SparePart.name.contains(search) | SparePart.part_code.contains(search) |
                SparePart.model_spec.contains(search) | SparePart.supplier.contains(search)
            )
        if category:
            query = query.filter_by(category=category)
        if stock_status == 'low':
            query = query.filter(SparePart.stock_quantity <= SparePart.warning_stock, SparePart.stock_quantity > 0)
        elif stock_status == 'out':
            query = query.filter(SparePart.stock_quantity <= 0)
        elif stock_status == 'warning':
            query = query.filter(SparePart.stock_quantity <= SparePart.safety_stock)
        parts = query.limit(200).all()
        categories = db.session.query(SparePart.category).filter(SparePart.category != '').distinct().all()
        categories = [c[0] for c in categories]

    return render_template('spare_parts/index.html',
                           parts=parts,
                           categories=categories,
                           search=search,
                           current_category=category,
                           stock_status=stock_status,
                           has_filter=has_filter)


@spare_parts_bp.route('/add', methods=['POST'])
@login_required
@admin_required
def add():
    part = SparePart(
        part_code=request.form.get('part_code', ''),
        name=request.form.get('name', ''),
        model_spec=request.form.get('model_spec', ''),
        unit=request.form.get('unit', '个'),
        category=request.form.get('category', ''),
        location=request.form.get('location', ''),
        stock_quantity=request.form.get('stock_quantity', 0, type=int),
        safety_stock=request.form.get('safety_stock', 0, type=int),
        warning_stock=request.form.get('warning_stock', 0, type=int),
        price=request.form.get('price', 0, type=float),
        supplier=request.form.get('supplier', ''),
        remark=request.form.get('remark', '')
    )
    if not part.part_code or not part.name:
        flash('备件编码和名称不能为空', 'error')
        return redirect(url_for('spare_parts.index'))
    if SparePart.query.filter_by(part_code=part.part_code).first():
        flash('备件编码已存在', 'error')
        return redirect(url_for('spare_parts.index'))
    db.session.add(part)
    db.session.commit()
    flash('备件添加成功', 'success')
    return redirect(url_for('spare_parts.index'))


@spare_parts_bp.route('/edit/<int:part_id>', methods=['POST'])
@login_required
@admin_required
def edit(part_id):
    part = SparePart.query.get_or_404(part_id)
    part.name = request.form.get('name', part.name)
    part.model_spec = request.form.get('model_spec', part.model_spec)
    part.unit = request.form.get('unit', part.unit)
    part.category = request.form.get('category', part.category)
    part.location = request.form.get('location', part.location)
    part.safety_stock = request.form.get('safety_stock', part.safety_stock, type=int)
    part.warning_stock = request.form.get('warning_stock', part.warning_stock, type=int)
    part.price = request.form.get('price', part.price, type=float)
    part.supplier = request.form.get('supplier', part.supplier)
    part.remark = request.form.get('remark', part.remark)
    db.session.commit()
    flash('备件信息已更新', 'success')
    return redirect(url_for('spare_parts.index'))


@spare_parts_bp.route('/delete/<int:part_id>', methods=['POST'])
@login_required
@admin_required
def delete(part_id):
    part = SparePart.query.get_or_404(part_id)
    part.is_active = False
    db.session.commit()
    flash('备件已停用', 'success')
    return redirect(url_for('spare_parts.index'))


@spare_parts_bp.route('/detail/<int:part_id>')
@login_required
def detail(part_id):
    part = SparePart.query.get_or_404(part_id)
    transactions = StockTransaction.query.filter_by(part_id=part_id).order_by(StockTransaction.created_at.desc()).limit(100).all()
    return render_template('spare_parts/detail.html', part=part, transactions=transactions)


@spare_parts_bp.route('/transaction', methods=['POST'])
@login_required
def transaction():
    part_id = request.form.get('part_id', type=int)
    trans_type = request.form.get('trans_type', 'out')
    quantity = request.form.get('quantity', 0, type=int)
    operator_name = request.form.get('operator_name', '').strip()
    purpose = request.form.get('purpose', '').strip()
    remark = request.form.get('remark', '').strip()

    if not part_id or quantity <= 0:
        flash('参数错误', 'error')
        return redirect(url_for('spare_parts.index'))

    part = SparePart.query.get_or_404(part_id)

    if trans_type == 'out':
        if part.stock_quantity < quantity:
            flash(f'库存不足，当前库存: {part.stock_quantity}', 'error')
            return redirect(url_for('spare_parts.index'))
        part.stock_quantity -= quantity
    else:
        location = request.form.get('location', '').strip()
        if not location:
            flash('请输入库位', 'error')
            return redirect(url_for('spare_parts.index'))
        part.location = location
        part.stock_quantity += quantity

    transaction = StockTransaction(
        part_id=part_id,
        user_id=current_user.id,
        trans_type=trans_type,
        quantity=quantity,
        operator_name=operator_name or current_user.real_name,
        purpose=purpose,
        remark=remark
    )
    db.session.add(transaction)
    db.session.commit()
    flash(f'{"出库" if trans_type == "out" else "入库"}成功，当前库存: {part.stock_quantity}', 'success')
    return redirect(url_for('spare_parts.index'))


@spare_parts_bp.route('/transactions')
@login_required
def transactions():
    query = StockTransaction.query.order_by(StockTransaction.created_at.desc())
    part_id = request.args.get('part_id', type=int)
    if part_id:
        query = query.filter_by(part_id=part_id)
    records = query.limit(200).all()
    return render_template('spare_parts/transactions.html', records=records)


@spare_parts_bp.route('/purchase', methods=['GET', 'POST'])
@login_required
def purchase():
    if request.method == 'POST':
        part_name = request.form.get('part_name', '').strip()
        model_spec = request.form.get('model_spec', '').strip()
        purpose = request.form.get('purpose', '').strip()
        quantity = request.form.get('quantity', 1, type=int)
        if not part_name:
            flash('请填写备件名称', 'error')
            return redirect(url_for('spare_parts.purchase'))
        image_path = ''
        if 'image' in request.files:
            file = request.files['image']
            if file and file.filename:
                ext = file.filename.rsplit('.', 1)[1].lower() if '.' in file.filename else ''
                if ext in {'jpg', 'jpeg', 'png', 'gif', 'bmp'}:
                    unique_name = f"purch_{uuid.uuid4().hex}.{ext}"
                    file.save(os.path.join(Config.UPLOAD_FOLDER, unique_name))
                    image_path = unique_name
        pr = PurchaseRequisition(
            user_id=current_user.id, part_name=part_name,
            model_spec=model_spec, quantity=quantity,
            purpose=purpose, image_path=image_path
        )
        db.session.add(pr)
        db.session.commit()
        flash('请购需求已提交', 'success')
        return redirect(url_for('spare_parts.purchase_list'))
    return render_template('spare_parts/purchase.html')


@spare_parts_bp.route('/purchase/list')
@login_required
def purchase_list():
    reqs = PurchaseRequisition.query.order_by(PurchaseRequisition.created_at.desc()).all()
    low_stock_parts = SparePart.query.filter(
        SparePart.is_active == True,
        SparePart.stock_quantity <= SparePart.warning_stock
    ).all()
    return render_template('spare_parts/purchase_list.html', reqs=reqs, low_stock_parts=low_stock_parts)


@spare_parts_bp.route('/purchase/<int:req_id>/status', methods=['POST'])
@login_required
@admin_required
def purchase_status(req_id):
    req = PurchaseRequisition.query.get_or_404(req_id)
    req.status = request.form.get('status', 'pending')
    db.session.commit()
    flash('状态已更新', 'success')
    return redirect(url_for('spare_parts.purchase_list'))


@spare_parts_bp.route('/api/search')
@login_required
def api_search():
    q = request.args.get('q', '').strip()
    if len(q) < 1:
        return jsonify([])
    parts = SparePart.query.filter(
        SparePart.is_active == True,
        SparePart.name.contains(q)
    ).order_by(SparePart.name).limit(10).all()
    return jsonify([{
        'id': p.id,
        'name': p.name,
        'model_spec': p.model_spec or '',
        'part_code': p.part_code,
    } for p in parts])
