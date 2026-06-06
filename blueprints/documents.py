import os
import uuid
from flask import Blueprint, render_template, request, redirect, url_for, flash, send_file, jsonify
from flask_login import login_required, current_user
from models import db, Document, DocumentCategory
from config import Config
from datetime import datetime
from blueprints.auth import admin_required
from werkzeug.utils import secure_filename

documents_bp = Blueprint('documents', __name__)


def allowed_file(filename):
    basename = os.path.basename(filename)
    return '.' in basename and basename.rsplit('.', 1)[1].lower() in Config.ALLOWED_EXTENSIONS


def safe_filename(filename):
    basename = os.path.basename(filename)
    if not basename or basename == '':
        basename = filename.replace('/', '_').replace('\\', '_')
    return basename


def get_file_type(filename):
    ext = filename.rsplit('.', 1)[1].lower() if '.' in filename else ''
    image_types = {'jpg', 'jpeg', 'png', 'gif', 'bmp', 'svg'}
    doc_types = {'pdf', 'doc', 'docx', 'xls', 'xlsx', 'ppt', 'pptx'}
    cad_types = {'dwg', 'dxf', 'step', 'stp', 'igs', 'iges'}
    archive_types = {'zip', 'rar', '7z', 'tar', 'gz'}
    if ext in image_types:
        return 'image'
    elif ext in doc_types:
        return 'document'
    elif ext in cad_types:
        return 'cad'
    elif ext in archive_types:
        return 'archive'
    return 'other'


def search_filesystem(search_term, base_path):
    """在文件系统中搜索匹配的文件"""
    results = []
    search_lower = search_term.lower()
    
    for root, dirs, files in os.walk(base_path):
        dirs[:] = [d for d in dirs if not d.startswith('.')]
        
        for filename in files:
            if filename.startswith('.'):
                continue
            
            # 检查文件名是否匹配
            if search_lower in filename.lower():
                rel_path = os.path.relpath(os.path.join(root, filename), base_path)
                filepath = os.path.join(root, filename)
                
                try:
                    stat = os.stat(filepath)
                    size = stat.st_size
                    if size < 1024:
                        size_str = f'{size}B'
                    elif size < 1024*1024:
                        size_str = f'{size/1024:.1f}KB'
                    elif size < 1024*1024*1024:
                        size_str = f'{size/(1024*1024):.1f}MB'
                    else:
                        size_str = f'{size/(1024*1024*1024):.1f}GB'
                    
                    results.append({
                        'type': 'filesystem',
                        'name': filename,
                        'path': rel_path.replace('\\', '/'),
                        'size': size_str,
                        'ext': filename.rsplit('.', 1)[1].lower() if '.' in filename else '',
                        'static_url': url_for('static', filename='uploads/' + rel_path.replace('\\', '/')),
                    })
                except Exception:
                    continue
    
    results.sort(key=lambda x: x['name'].lower())
    return results


@documents_bp.route('/')
@login_required
def index():
    category_id = request.args.get('category_id', type=int)
    search = request.args.get('search', '').strip()
    categories = DocumentCategory.query.order_by(DocumentCategory.sort_order).all()

    query = Document.query.order_by(Document.created_at.desc())
    if category_id:
        query = query.filter_by(category_id=category_id)
    
    # 数据库文档搜索
    db_documents = []
    if search:
        query = query.filter(Document.title.contains(search) | Document.original_filename.contains(search))
        db_documents = query.all()
    else:
        db_documents = query.all()
    
    # 文件系统搜索（仅在有搜索关键字时）
    fs_documents = []
    if search:
        fs_documents = search_filesystem(search, Config.UPLOAD_FOLDER)
    
    return render_template('documents/index.html',
                           documents=db_documents,
                           fs_documents=fs_documents,
                           categories=categories,
                           current_category_id=category_id,
                           search=search)


@documents_bp.route('/upload', methods=['POST'])
@login_required
def upload():
    category_id = request.form.get('category_id', type=int)
    title = request.form.get('title', '').strip()
    description = request.form.get('description', '').strip()
    files = request.files.getlist('files')

    if not category_id or not files or not files[0].filename:
        flash('请选择分类并上传文件', 'error')
        return redirect(url_for('documents.index'))

    category = DocumentCategory.query.get(category_id)
    if not category:
        flash('分类不存在', 'error')
        return redirect(url_for('documents.index'))

    count = 0
    for file in files:
        if file and allowed_file(file.filename):
            orig_name = safe_filename(file.filename)
            unique_name = f"{uuid.uuid4().hex}_{orig_name}"
            filepath = os.path.join(Config.UPLOAD_FOLDER, unique_name)
            file.save(filepath)
            file_size = os.path.getsize(filepath)

            t = title if title else os.path.splitext(orig_name)[0]
            doc = Document(
                category_id=category_id,
                title=t,
                filename=unique_name,
                original_filename=orig_name,
                file_size=file_size,
                file_type=get_file_type(file.filename),
                uploader_id=current_user.id,
                description=description
            )
            db.session.add(doc)
            count += 1

    db.session.commit()
    flash(f'成功上传 {count} 个文件', 'success')
    return redirect(url_for('documents.index', category_id=category_id))


@documents_bp.route('/download/<int:doc_id>')
@login_required
def download(doc_id):
    doc = Document.query.get_or_404(doc_id)
    doc.download_count += 1
    db.session.commit()
    filepath = os.path.join(Config.UPLOAD_FOLDER, doc.filename)
    if not os.path.exists(filepath):
        flash('文件不存在', 'error')
        return redirect(url_for('documents.index'))
    return send_file(filepath, download_name=doc.original_filename, as_attachment=True)


@documents_bp.route('/view/<int:doc_id>')
@login_required
def view(doc_id):
    doc = Document.query.get_or_404(doc_id)
    filepath = os.path.join(Config.UPLOAD_FOLDER, doc.filename)
    if not os.path.exists(filepath):
        flash('文件不存在', 'error')
        return redirect(url_for('documents.index'))
    image_types = {'jpg', 'jpeg', 'png', 'gif', 'bmp', 'svg'}
    ext = doc.original_filename.rsplit('.', 1)[1].lower() if '.' in doc.original_filename else ''
    if ext in image_types:
        return send_file(filepath)
    return send_file(filepath, download_name=doc.original_filename)


@documents_bp.route('/delete/<int:doc_id>', methods=['POST'])
@login_required
def delete(doc_id):
    doc = Document.query.get_or_404(doc_id)
    if not current_user.is_admin and doc.uploader_id != current_user.id:
        flash('无权删除', 'error')
        return redirect(url_for('documents.index'))
    filepath = os.path.join(Config.UPLOAD_FOLDER, doc.filename)
    if os.path.exists(filepath):
        os.remove(filepath)
    db.session.delete(doc)
    db.session.commit()
    flash('文件已删除', 'success')
    return redirect(url_for('documents.index'))


@documents_bp.route('/categories', methods=['GET', 'POST'])
@login_required
@admin_required
def manage_categories():
    if request.method == 'POST':
        name = request.form.get('name', '').strip()
        description = request.form.get('description', '').strip()
        sort_order = request.form.get('sort_order', 0, type=int)
        if name:
            cat = DocumentCategory(name=name, description=description, sort_order=sort_order)
            db.session.add(cat)
            db.session.commit()
            flash('分类添加成功', 'success')
        return redirect(url_for('documents.manage_categories'))
    categories = DocumentCategory.query.order_by(DocumentCategory.sort_order).all()
    return render_template('documents/categories.html', categories=categories)


@documents_bp.route('/categories/delete/<int:cat_id>', methods=['POST'])
@login_required
@admin_required
def delete_category(cat_id):
    cat = DocumentCategory.query.get_or_404(cat_id)
    if cat.documents.count() > 0:
        flash('该分类下还有文件，无法删除', 'error')
        return redirect(url_for('documents.manage_categories'))
    db.session.delete(cat)
    db.session.commit()
    flash('分类已删除', 'success')
    return redirect(url_for('documents.manage_categories'))


@documents_bp.route('/browse/')
@documents_bp.route('/browse/<path:subpath>')
@login_required
def browse(subpath=''):
    base = Config.UPLOAD_FOLDER
    safe_sub = subpath.replace('\\', '/').strip('/')
    current_dir = os.path.join(base, safe_sub) if safe_sub else base

    real_base = os.path.realpath(base)
    real_current = os.path.realpath(current_dir)
    if not real_current.startswith(real_base + os.sep) and real_current != real_base:
        flash('路径不允许', 'error')
        return redirect(url_for('documents.browse'))

    folders = []
    files = []
    try:
        for entry in sorted(os.scandir(current_dir), key=lambda e: (not e.is_dir(), e.name.lower())):
            if entry.name.startswith('.'):
                continue
            rel = (safe_sub + '/' + entry.name).strip('/') if safe_sub else entry.name
            if entry.is_dir():
                folders.append({'name': entry.name, 'path': rel})
            elif entry.is_file():
                stat = entry.stat()
                size = stat.st_size
                if size < 1024:
                    size_str = f'{size}B'
                elif size < 1024*1024:
                    size_str = f'{size/1024:.1f}KB'
                elif size < 1024*1024*1024:
                    size_str = f'{size/(1024*1024):.1f}MB'
                else:
                    size_str = f'{size/(1024*1024*1024):.1f}GB'
                files.append({
                    'name': entry.name,
                    'path': rel,
                    'size': size_str,
                    'ext': entry.name.rsplit('.', 1)[1].lower() if '.' in entry.name else '',
                    'static_url': url_for('static', filename='uploads/' + rel.replace('\\', '/')),
                })
    except PermissionError:
        flash('无权限访问', 'error')
        return redirect(url_for('documents.browse'))

    breadcrumbs = []
    if safe_sub:
        parts = safe_sub.split('/')
        for i in range(len(parts)):
            breadcrumbs.append({
                'name': parts[i],
                'path': '/'.join(parts[:i+1]),
            })

    return render_template('documents/browse.html',
                           folders=folders, files=files,
                           subpath=safe_sub, breadcrumbs=breadcrumbs)
