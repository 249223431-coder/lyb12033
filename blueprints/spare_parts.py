from flask import Blueprint, render_template, request, redirect, url_for, flash, jsonify
from flask_login import login_required, current_user
from models import db, SparePart, StockTransaction, PurchaseRequisition
from blueprints.auth import admin_required
import os, uuid
from pathlib import Path
from config import Config

spare_parts_bp = Blueprint('spare_parts', __name__)

# ========================================
# 物料表格缓存（ERP物料库）
# ========================================
import openpyxl
import threading

_material_cache = []
_material_cache_loaded = False
_material_cache_lock = threading.Lock()

def _load_material_cache():
    """加载物料表格到内存缓存"""
    global _material_cache, _material_cache_loaded
    if _material_cache_loaded:
        return
    
    with _material_cache_lock:
        if _material_cache_loaded:
            return
        
        excel_path = os.path.join(os.path.dirname(os.path.dirname(__file__)), '物料.xlsx')
        if not os.path.exists(excel_path):
            _material_cache_loaded = True
            return
        
        try:
            wb = openpyxl.load_workbook(excel_path, read_only=True, data_only=True)
            ws = wb.active
            
            # 跳过标题行，从第2行开始
            for row in ws.iter_rows(min_row=2, values_only=True):
                if not row or not row[0]:  # 跳过空行
                    continue
                
                material_id = str(row[0]).strip() if row[0] else ''  # 物料号
                material_desc = str(row[4]).strip() if row[4] else ''  # 物料描述
                price = str(row[3]).strip() if row[3] else ''  # 价格
                
                # 跳过标记为删除的物料
                if '删除' in material_desc:
                    continue
                
                # 清理HTML实体
                material_desc = material_desc.replace('&#x', '\\u').replace(';', '')
                
                if material_id and material_desc:
                    _material_cache.append({
                        'id': material_id,
                        'name': material_desc,
                        'price': price,
                        'material_group': str(row[7]).strip() if row[7] else ''  # 物料组
                    })
            
            wb.close()
            _material_cache_loaded = True
            print(f'[物料缓存] 已加载 {len(_material_cache)} 条物料记录')
            
        except Exception as e:
            print(f'[物料缓存] 加载失败: {e}')
            _material_cache_loaded = True

def _search_material_cache(keyword):
    """搜索物料缓存"""
    if not _material_cache_loaded:
        _load_material_cache()
    
    keyword = keyword.lower()
    results = []
    
    for item in _material_cache:
        # 搜索物料号或物料描述
        if keyword in item['id'].lower() or keyword in item['name'].lower():
            results.append({
                'id': item['id'],
                'name': item['name'],
                'part_code': item['id'],
                'price': item['price'],
                'material_group': item['material_group'],
                'source': '物料库'
            })
            if len(results) >= 20:  # 限制返回数量
                break
    
    return results

# 启动时异步加载物料缓存
threading.Thread(target=_load_material_cache, daemon=True).start()


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
        part_code = request.form.get('part_code', '').strip()
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
            part_code=part_code, model_spec=model_spec,
            quantity=quantity, purpose=purpose, image_path=image_path
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
    
    results = []
    
    # 1. 搜索备件库
    parts = SparePart.query.filter(
        SparePart.is_active == True,
        SparePart.name.contains(q)
    ).order_by(SparePart.name).limit(10).all()
    
    for p in parts:
        results.append({
            'id': p.id,
            'name': p.name,
            'model_spec': p.model_spec or '',
            'part_code': p.part_code,
            'source': '备件库'
        })
    
    # 2. 搜索物料库
    material_results = _search_material_cache(q)
    results.extend(material_results)
    
    return jsonify(results)


# ========================================
# BOM设备型号搜索功能（新增）
# ========================================

import xlrd
import re
import os as _os
import time
import threading

_devnull = open(_os.devnull, 'w')

def _open_xls(path):
    """静默打开Excel，抑制损坏文件的OLE2警告"""
    return xlrd.open_workbook(str(path), logfile=_devnull)

# ---- BOM数据内存缓存 ----
_bom_cache_lock = threading.Lock()
_bom_cache = {}   # {filename: {'mtime': ..., 'data': [...]}}
_bom_cache_ready = False

def _load_bom_cache():
    """预加载所有BOM文件到内存缓存"""
    global _bom_cache, _bom_cache_ready
    bom_dir = Path(r"E:\team\team\static\uploads\BOM_清单")
    if not bom_dir.exists():
        _bom_cache_ready = True
        return
    
    new_cache = {}
    for bom_file in bom_dir.glob("*.xls*"):
        try:
            mtime = bom_file.stat().st_mtime
            # 如果已有缓存且文件未修改，跳过
            if bom_file.name in _bom_cache and _bom_cache[bom_file.name].get('mtime') == mtime:
                new_cache[bom_file.name] = _bom_cache[bom_file.name]
                continue
            
            wb = _open_xls(bom_file)
            ws = wb.sheet_by_index(0)
            
            # 表头
            headers = []
            header_row_idx = -1
            for hr in range(min(15, ws.nrows)):
                hd = []
                for c in range(min(ws.ncols, 15)):
                    v = str(ws.cell_value(hr, c)).strip()
                    hd.append(v if v else f'列{c}')
                if any('No.' in h or 'Item' in h or 'Part' in h for h in hd):
                    headers = hd
                    header_row_idx = hr
                    break
            
            start_row = header_row_idx + 1 if header_row_idx >= 0 else 10
            
            # 读取所有行
            all_rows = {}
            for row_idx in range(start_row, ws.nrows):
                row_data = []
                for c in range(min(ws.ncols, 15)):
                    cv = ws.cell_value(row_idx, c)
                    if isinstance(cv, float) and cv == int(cv):
                        row_data.append(str(int(cv)))
                    else:
                        row_data.append(str(cv))
                if any(c.strip() for c in row_data):
                    all_rows[row_idx] = row_data
            
            # 顶部信息行
            top_info_row = None
            for row_idx in sorted(all_rows.keys()):
                rd = all_rows[row_idx]
                if not rd[0].strip() and (rd[1].strip() or rd[3].strip() or rd[4].strip()):
                    top_info_row = [str(c) if c else '' for c in rd]
                    break
            
            wb.release_resources()
            
            new_cache[bom_file.name] = {
                'mtime': mtime,
                'headers': [str(h) if h else '' for h in headers],
                'all_rows': {k: [str(c) if c else '' for c in v] for k, v in all_rows.items()},
                'top_info_row': top_info_row,
            }
        except Exception:
            continue
    
    with _bom_cache_lock:
        _bom_cache = new_cache
        _bom_cache_ready = True

def _get_bom_cache():
    """获取BOM缓存，首次调用时加载"""
    global _bom_cache_ready
    if not _bom_cache_ready:
        _load_bom_cache()
    return _bom_cache

def _check_bom_updates():
    """定期检查BOM文件更新（每5分钟）"""
    while True:
        time.sleep(300)
        try:
            _load_bom_cache()
        except Exception:
            pass

# 启动后台缓存检查线程
_bom_update_thread = threading.Thread(target=_check_bom_updates, daemon=True)
_bom_update_thread.start()


@spare_parts_bp.route('/bom_search')
@login_required
def bom_search():
    """BOM设备型号搜索页面 - 支持GET参数直接搜索 (使用缓存加速)"""
    device_id = request.args.get('device_id', '').strip()
    results = []

    if device_id and len(device_id) >= 2:
        bom_cache = _get_bom_cache()
        
        for filename, cache_entry in bom_cache.items():
            try:
                headers = cache_entry['headers']
                all_rows = cache_entry['all_rows']
                top_info_row = cache_entry['top_info_row']

                # 搜索匹配
                for row_idx, row_data in all_rows.items():
                    for ci in range(len(row_data)):
                        if row_data[ci].strip() and device_id.upper() in row_data[ci].upper():
                            # 找上下文零件行
                            prev_part = None
                            next_part = None
                            prev_part_idx = None
                            next_part_idx = None
                            non_empty_count = sum(1 for c in row_data if c.strip())
                            if non_empty_count <= 3:
                                for prev_idx in range(row_idx - 1, max(row_idx - 5, -1), -1):
                                    if prev_idx in all_rows:
                                        pr = all_rows[prev_idx]
                                        if sum(1 for c in pr if c.strip()) > 3:
                                            prev_part = pr
                                            prev_part_idx = prev_idx
                                            break
                                for next_idx in range(row_idx + 1, min(row_idx + 5, max(all_rows.keys()) + 1)):
                                    if next_idx in all_rows:
                                        nr = all_rows[next_idx]
                                        if sum(1 for c in nr if c.strip()) > 3:
                                            next_part = nr
                                            next_part_idx = next_idx
                                            break

                            def merge_continuations(part_data, part_idx, match_idx):
                                if part_idx is None:
                                    return part_data
                                result = list(part_data)
                                extras = []
                                for mid_idx in range(part_idx + 1, match_idx):
                                    if mid_idx in all_rows:
                                        mr = all_rows[mid_idx]
                                        if not mr[0].strip() and not mr[1].strip() and mr[4].strip():
                                            extras.append(mr[4])
                                if extras:
                                    result[4] = result[4] + ' / ' + ' / '.join(extras)
                                return result

                            prev_part = merge_continuations(prev_part, prev_part_idx, row_idx)
                            next_part = merge_continuations(next_part, next_part_idx, row_idx) if next_part_idx else None

                            sections = []
                            
                            if top_info_row:
                                sec_cells = list(top_info_row)
                                if any(c.strip() for c in sec_cells):
                                    sections.append({'title': '设备型号', 'cells': sec_cells, 'color': '#00897b'})

                            match_cells = list(row_data)
                            for i in range(len(match_cells)):
                                if not match_cells[i].strip():
                                    if prev_part and prev_part[i].strip():
                                        match_cells[i] = prev_part[i]
                                    elif next_part and next_part[i].strip():
                                        match_cells[i] = next_part[i]
                            sections.append({'title': '匹配的设备', 'cells': match_cells, 'color': '#e65100', 'matched_col': ci})

                            if prev_part:
                                sec_cells = list(prev_part)
                                sections.append({'title': '关联零件', 'cells': sec_cells, 'color': '#666'})
                            elif next_part:
                                sec_cells = list(next_part)
                                sections.append({'title': '关联零件', 'cells': sec_cells, 'color': '#666'})

                            results.append({
                                'file': filename,
                                'headers': headers,
                                'sections': sections,
                            })
                            break

                if len(results) > 200:
                    break
            except:
                continue

    # 生成结果HTML
    result_html = ''
    if device_id and results:
        result_html += f'<div class="bom-result-count">找到 <strong>{len(results)}</strong> 条结果 (搜索词: {device_id})</div>'
        for item in results:
            bom_file_path = 'BOM_清单/' + item['file']
            result_html += '<div class="bom-card"><div class="card-file"><i class="bi bi-file-earmark-excel"></i> <a href="/documents/viewer?file=' + bom_file_path + '&title=' + item['file'] + '" target="_blank" style="color:#00897b;text-decoration:none;">' + item['file'] + '</a></div>'
            for section in item['sections']:
                result_html += f'<div class="card-section-title" style="color:{section["color"]};">【{section["title"]}】</div>'
                for i, cell in enumerate(section['cells']):
                    if cell.strip():
                        label = item['headers'][i] if i < len(item['headers']) else f'列{i}'
                        hl = ' highlight' if section.get('matched_col') == i else ''
                        result_html += f'<div class="card-row"><span class="card-label">{label}:</span><span class="card-value{hl}">{cell}</span></div>'
                result_html += ''
            result_html += '</div>'
    elif device_id:
        result_html = f'<div class="bom-no-result"><i class="bi bi-search" style="font-size:48px;color:#ddd;display:block;margin-bottom:10px;"></i><p>未找到匹配 "<strong>{device_id}</strong>" 的设备编号</p></div>'
    else:
        result_html = '<div style="text-align:center;padding:20px;color:#ccc;font-size:12px;">V4-PYTHON生成-请输入设备编号搜索</div>'

    return f'''<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no">
    <title>设备型号搜索 - 班组管理</title>
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css" rel="stylesheet">
    <link href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.2/font/bootstrap-icons.css" rel="stylesheet">
    <link href="/static/css/style.css" rel="stylesheet">
<style>
.bom-header {{
    margin: -12px -12px 12px -12px; padding: 16px 16px 12px;
    background: linear-gradient(135deg, #00897b 0%, #26a69a 100%); color: #fff; border-radius: 0 0 16px 16px;
}}
.bom-header h1 {{ font-size: 19px; font-weight: 700; margin: 0; }}
.bom-header-sub {{ font-size: 11px; opacity: 0.8; margin-top: 2px; }}
.bom-tabs {{ display: flex; gap: 8px; padding: 12px 16px; background: #f5f5f5; }}
.bom-tab {{ padding: 8px 16px; border-radius: 20px; text-decoration: none; color: #666; font-weight: 500; }}
.bom-tab.active {{ background: #00897b; color: white; }}
.bom-search-box {{ background: #fff; border-radius: 16px; padding: 16px; margin-bottom: 12px; box-shadow: 0 2px 8px rgba(0,0,0,0.06); }}
.bom-search-input {{ width: 100%; padding: 12px 16px; border: 2px solid #e0e0e0; border-radius: 12px; font-size: 15px; outline: none; box-sizing: border-box; }}
.bom-search-input:focus {{ border-color: #00897b; }}
.bom-search-btn {{ width: 100%; padding: 12px; margin-top: 10px; background: linear-gradient(135deg, #00897b 0%, #26a69a 100%); color: white; border: none; border-radius: 12px; font-size: 15px; font-weight: 600; cursor: pointer; }}
.bom-result-count {{ padding: 10px 16px; background: #e8f5e9; border-radius: 10px; margin-bottom: 12px; font-size: 13px; color: #2e7d32; }}
.bom-no-result {{ text-align: center; padding: 60px 20px; color: #999; }}
.bom-card {{ background: #fff; border-radius: 12px; padding: 14px; margin-bottom: 10px; box-shadow: 0 2px 8px rgba(0,0,0,0.06); border-left: 4px solid #00897b; }}
.bom-card .card-file {{ font-size: 11px; color: #888; margin-bottom: 8px; padding-bottom: 8px; border-bottom: 1px solid #f0f0f0; }}
.bom-card .card-section-title {{ font-size: 11px; font-weight: 700; padding: 4px 0; margin: 6px 0 2px; border-bottom: 1px dashed #e0e0e0; }}
.bom-card .card-row {{ display: flex; padding: 3px 0; font-size: 12px; line-height: 1.6; }}
.bom-card .card-label {{ color: #888; min-width: 70px; flex-shrink: 0; font-weight: 500; }}
.bom-card .card-value {{ color: #333; word-break: break-all; }}
.bom-card .card-value.highlight {{ background: #fff39c; padding: 1px 4px; border-radius: 3px; font-weight: bold; }}
</style>
</head>
<body>
<div class="app-container">
    <header class="app-header">
        <div class="header-left"><span class="header-logo">🏭</span><span class="header-title">班组管理</span></div>
        <div class="header-right">
            <a href="/notifications" class="header-icon"><i class="bi bi-bell"></i></a>
            <div class="dropdown">
                <a href="#" class="header-icon dropdown-toggle" data-bs-toggle="dropdown"><i class="bi bi-person-circle"></i></a>
            </div>
        </div>
    </header>
    <main class="app-content">
        <div class="bom-header"><h1><i class="bi bi-cpu"></i> 设备型号搜索</h1><div class="bom-header-sub">从BOM清单中搜索设备信息</div></div>
        <div class="bom-tabs">
            <a href="/spare_parts" class="bom-tab"><i class="bi bi-box-seam"></i> 备件库存</a>
            <a href="/spare_parts/bom_search" class="bom-tab active"><i class="bi bi-cpu"></i> 设备型号</a>
        </div>
        <div class="bom-search-box">
            <form method="get" action="/spare_parts/bom_search">
                <input type="text" name="device_id" class="bom-search-input" placeholder="输入设备编号，如 AB15PM445" value="{device_id}" autocomplete="off">
                <button type="submit" class="bom-search-btn"><i class="bi bi-search"></i> 搜索</button>
            </form>
        </div>
        {result_html}
    </main>
    <nav class="bottom-nav">
        <a href="/" class="nav-item"><i class="bi bi-house-door"></i><span>首页</span></a>
        <a href="/attendance" class="nav-item"><i class="bi bi-calendar-check"></i><span>考勤</span></a>
        <a href="/inspection" class="nav-item"><i class="bi bi-clipboard-check"></i><span>点检</span></a>
        <a href="/documents" class="nav-item"><i class="bi bi-folder"></i><span>资料</span></a>
        <a href="#" class="nav-item" data-bs-toggle="modal" data-bs-target="#moreMenuModal"><i class="bi bi-grid"></i><span>更多</span></a>
    </nav>
</div>
<script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js"></script>
<script>document.querySelector('.bom-search-input').focus();</script>
</body>
</html>'''


@spare_parts_bp.route('/api/bom/search')
@login_required
def api_bom_search():
    """搜索设备型号API - 搜索所有列，返回整行数据 (使用缓存)"""
    device_id = request.args.get('device_id', '').strip()

    if not device_id or len(device_id) < 2:
        return jsonify({'error': '请输入至少2个字符的设备编号'})

    results = []
    bom_cache = _get_bom_cache()

    for filename, cache_entry in bom_cache.items():
        try:
            headers = cache_entry['headers']
            all_rows = cache_entry['all_rows']

            for row_idx, row_data in all_rows.items():
                if not any(cell.strip() for cell in row_data):
                    continue

                for col_idx in range(len(row_data)):
                    cell_value = row_data[col_idx]
                    if not cell_value.strip():
                        continue
                    if device_id.upper() in cell_value.upper():
                        results.append({
                            'file': filename,
                            'row_index': row_idx,
                            'headers': headers,
                            'row_data': row_data,
                            'matched_col': col_idx,
                            'matched_value': cell_value
                        })
                        break

            if len(results) > 500:
                break

        except:
            continue

    return jsonify({
        'device_id': device_id,
        'count': len(results),
        'results': results
    })


@spare_parts_bp.route('/api/bom/list')
@login_required
def api_bom_list():
    """获取所有唯一设备编号列表（用于下拉选择）(使用缓存)"""
    device_ids = set()
    pattern = re.compile(r'AB\d{2}[A-Z][A-Z]\d{3,}[A-Z0-9-]*', re.IGNORECASE)
    bom_cache = _get_bom_cache()

    for cache_entry in bom_cache.values():
        try:
            all_rows = cache_entry['all_rows']

            for row_data in all_rows.values():
                for col_idx in [4, 6, 8]:
                    if col_idx >= len(row_data):
                        continue
                    cell_str = row_data[col_idx]
                    if cell_str:
                        if col_idx == 8 and cell_str.strip().upper().startswith('AB'):
                            device_ids.add(cell_str.strip())
                        matches = pattern.findall(cell_str)
                        for match in matches:
                            device_ids.add(match.upper())
        except:
            continue

    return jsonify(sorted(list(device_ids)))


@spare_parts_bp.route('/api/bom/link_spare')
@login_required
def api_bom_link_spare():
    """查询物料编号对应的仓库备件"""
    item_code = request.args.get('item_code', '').strip()

    if not item_code:
        return jsonify({'found': False})

    # 在仓库备件中搜索（不修改任何数据）
    spare = SparePart.query.filter(
        SparePart.is_active == True,
        (SparePart.part_code == item_code) | (SparePart.model_spec.contains(item_code))
    ).first()

    if spare:
        return jsonify({
            'found': True,
            'spare': {
                'id': spare.id,
                'name': spare.name,
                'part_code': spare.part_code,
                'stock_quantity': spare.stock_quantity,
                'location': spare.location,
                'unit': spare.unit,
                'status': '有库存' if spare.stock_quantity > 0 else '库存不足'
            }
        })

    return jsonify({'found': False})
