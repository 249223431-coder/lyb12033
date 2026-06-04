import os
import uuid
from flask import Blueprint, render_template, request, redirect, url_for, flash
from flask_login import login_required, current_user
from models import db, CollectionForm, CollectionField, CollectionSubmission, CollectionValue
from blueprints.auth import admin_required
from config import Config

data_search_bp = Blueprint('data_search', __name__)


@data_search_bp.route('/')
@login_required
def index():
    forms = CollectionForm.query.filter_by(is_active=True).order_by(CollectionForm.sort_order).all()
    return render_template('data_search/index.html', forms=forms)


@data_search_bp.route('/form/<int:form_id>')
@login_required
def fill_form(form_id):
    form = CollectionForm.query.get_or_404(form_id)
    if not form.is_active:
        flash('该采集表已停用', 'error')
        return redirect(url_for('data_search.index'))
    fields = form.fields.order_by(CollectionField.sort_order).all()
    return render_template('data_search/form.html', form=form, fields=fields)


@data_search_bp.route('/form/<int:form_id>/submit', methods=['POST'])
@login_required
def submit_form(form_id):
    form = CollectionForm.query.get_or_404(form_id)
    if not form.is_active:
        flash('该采集表已停用', 'error')
        return redirect(url_for('data_search.index'))

    submission = CollectionSubmission(
        form_id=form_id,
        user_id=current_user.id
    )
    db.session.add(submission)
    db.session.flush()

    for field in form.fields.all():
        if field.field_type == 'file':
            file = request.files.get(f'field_{field.id}')
            if file and file.filename:
                ext = file.filename.rsplit('.', 1)[1].lower() if '.' in file.filename else ''
                allowed = {'jpg', 'jpeg', 'png', 'gif', 'bmp', 'pdf', 'doc', 'docx', 'xls', 'xlsx'}
                if ext in allowed:
                    unique_name = f"collect_{uuid.uuid4().hex}.{ext}"
                    file.save(os.path.join(Config.UPLOAD_FOLDER, unique_name))
                    value = unique_name
                elif ext:
                    flash(f'字段「{field.label}」的文件格式不支持，已跳过', 'warning')
                    continue
                else:
                    continue
            else:
                if field.required:
                    flash(f'字段「{field.label}」为必填项', 'error')
                    db.session.rollback()
                    return redirect(url_for('data_search.fill_form', form_id=form_id))
                continue
        elif field.field_type == 'checkbox':
            vals = request.form.getlist(f'field_{field.id}')
            value = '\n'.join(vals) if vals else ''
            if field.required and not value:
                flash(f'字段「{field.label}」为必填项', 'error')
                db.session.rollback()
                return redirect(url_for('data_search.fill_form', form_id=form_id))
        else:
            value = request.form.get(f'field_{field.id}', '').strip()
            if field.required and not value:
                flash(f'字段「{field.label}」为必填项', 'error')
                db.session.rollback()
                return redirect(url_for('data_search.fill_form', form_id=form_id))

        cv = CollectionValue(
            submission_id=submission.id,
            field_id=field.id,
            value=value
        )
        db.session.add(cv)

    db.session.commit()
    flash('提交成功', 'success')
    return redirect(url_for('data_search.index'))


@data_search_bp.route('/my')
@login_required
def my_submissions():
    submissions = CollectionSubmission.query.filter_by(user_id=current_user.id)\
        .order_by(CollectionSubmission.created_at.desc()).all()
    return render_template('data_search/my_submissions.html', submissions=submissions)


@data_search_bp.route('/manage')
@login_required
@admin_required
def manage():
    forms = CollectionForm.query.order_by(CollectionForm.sort_order).all()
    return render_template('data_search/manage.html', forms=forms)


@data_search_bp.route('/manage/create', methods=['POST'])
@login_required
@admin_required
def create_form():
    title = request.form.get('title', '').strip()
    description = request.form.get('description', '').strip()
    icon = request.form.get('icon', 'bi-clipboard').strip()
    if not title:
        flash('请输入表单标题', 'error')
        return redirect(url_for('data_search.manage'))
    form = CollectionForm(
        title=title,
        description=description,
        icon=icon,
        created_by=current_user.id,
        sort_order=CollectionForm.query.count()
    )
    db.session.add(form)
    db.session.commit()
    flash('采集表创建成功，请添加字段', 'success')
    return redirect(url_for('data_search.edit_form', form_id=form.id))


@data_search_bp.route('/manage/<int:form_id>/edit', methods=['GET', 'POST'])
@login_required
@admin_required
def edit_form(form_id):
    form = CollectionForm.query.get_or_404(form_id)
    if request.method == 'POST':
        form.title = request.form.get('title', '').strip()
        form.description = request.form.get('description', '').strip()
        form.icon = request.form.get('icon', 'bi-clipboard').strip()
        form.is_active = request.form.get('is_active') == 'on'
        db.session.commit()
        flash('采集表已更新', 'success')
        return redirect(url_for('data_search.manage'))
    fields = form.fields.order_by(CollectionField.sort_order).all()
    return render_template('data_search/edit_form.html', form=form, fields=fields)


@data_search_bp.route('/manage/<int:form_id>/delete', methods=['POST'])
@login_required
@admin_required
def delete_form(form_id):
    form = CollectionForm.query.get_or_404(form_id)
    CollectionValue.query.filter(
        CollectionValue.submission_id.in_(
            db.session.query(CollectionSubmission.id).filter_by(form_id=form_id)
        )
    ).delete(synchronize_session='fetch')
    CollectionSubmission.query.filter_by(form_id=form_id).delete()
    CollectionField.query.filter_by(form_id=form_id).delete()
    db.session.delete(form)
    db.session.commit()
    flash('采集表已删除', 'success')
    return redirect(url_for('data_search.manage'))


@data_search_bp.route('/manage/<int:form_id>/field/add', methods=['POST'])
@login_required
@admin_required
def add_field(form_id):
    form = CollectionForm.query.get_or_404(form_id)
    label = request.form.get('label', '').strip()
    field_type = request.form.get('field_type', 'text')
    if not label:
        flash('请输入字段名称', 'error')
        return redirect(url_for('data_search.edit_form', form_id=form_id))
    field = CollectionField(
        form_id=form_id,
        label=label,
        field_type=field_type,
        options=request.form.get('options', '').strip(),
        placeholder=request.form.get('placeholder', '').strip(),
        required=request.form.get('required') == 'on',
        sort_order=form.fields.count()
    )
    db.session.add(field)
    db.session.commit()
    flash(f'字段「{label}」已添加', 'success')
    return redirect(url_for('data_search.edit_form', form_id=form_id))


@data_search_bp.route('/manage/field/<int:field_id>/delete', methods=['POST'])
@login_required
@admin_required
def delete_field(field_id):
    field = CollectionField.query.get_or_404(field_id)
    form_id = field.form_id
    CollectionValue.query.filter_by(field_id=field_id).delete()
    db.session.delete(field)
    db.session.commit()
    flash('字段已删除', 'success')
    return redirect(url_for('data_search.edit_form', form_id=form_id))


@data_search_bp.route('/manage/<int:form_id>/submissions')
@login_required
@admin_required
def view_submissions(form_id):
    form = CollectionForm.query.get_or_404(form_id)
    submissions = form.submissions.order_by(CollectionSubmission.created_at.desc()).all()
    fields = form.fields.order_by(CollectionField.sort_order).all()

    data = []
    for sub in submissions:
        row = {'sub': sub}
        for field in fields:
            val = CollectionValue.query.filter_by(submission_id=sub.id, field_id=field.id).first()
            row[field.id] = val.value if val else ''
        data.append(row)

    return render_template('data_search/submissions.html', form=form, fields=fields, data=data)
