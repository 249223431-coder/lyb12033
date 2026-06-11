import os
import shutil
import zipfile
import argparse
from datetime import datetime

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
BACKUP_DIR = r'D:\team_backups'


def backup():
    timestamp = datetime.now().strftime('%Y%m%d_%H%M')
    os.makedirs(BACKUP_DIR, exist_ok=True)
    count = 0

    # 1. 备份数据库
    db_path = os.path.join(BASE_DIR, 'instance', 'team.db')
    if os.path.exists(db_path):
        dst = os.path.join(BACKUP_DIR, f'team_{timestamp}.db')
        shutil.copy2(db_path, dst)
        count += 1
        print(f'[OK] 数据库备份 → {dst}')
    else:
        db_path = os.path.join(BASE_DIR, 'app.db')
        if os.path.exists(db_path):
            dst = os.path.join(BACKUP_DIR, f'team_{timestamp}.db')
            shutil.copy2(db_path, dst)
            count += 1
            print(f'[OK] 数据库备份 → {dst}')

    # 2. 备份上传文件（压缩）
    uploads_dir = os.path.join(BASE_DIR, 'static', 'uploads')
    if os.path.exists(uploads_dir) and os.listdir(uploads_dir):
        zip_path = os.path.join(BACKUP_DIR, f'uploads_{timestamp}.zip')
        with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as zf:
            for root, dirs, files in os.walk(uploads_dir):
                for f in files:
                    fp = os.path.join(root, f)
                    arcname = os.path.relpath(fp, uploads_dir)
                    zf.write(fp, arcname)
        count += 1
        print(f'[OK] 上传文件备份 → {zip_path}')

    # 3. 备份Excel文件
    for fname in ['停机计划.xlsx']:
        fpath = os.path.join(BASE_DIR, fname)
        if os.path.exists(fpath):
            name, ext = os.path.splitext(fname)
            dst = os.path.join(BACKUP_DIR, f'{name}_{timestamp}{ext}')
            shutil.copy2(fpath, dst)
            count += 1
            print(f'[OK] Excel备份 → {dst}')

    # 4. 清理超过30天的旧备份
    cleaned = 0
    for f in os.listdir(BACKUP_DIR):
        fpath = os.path.join(BACKUP_DIR, f)
        if os.path.isfile(fpath):
            age = datetime.now().timestamp() - os.path.getmtime(fpath)
            if age > 30 * 86400:
                os.remove(fpath)
                cleaned += 1
    if cleaned:
        print(f'[清理] 已删除 {cleaned} 个超过30天的旧备份')

    print(f'\n备份完成，共 {count} 个文件 → {BACKUP_DIR}')
    return count


def restore(backup_file):
    if not os.path.exists(backup_file):
        print(f'错误：备份文件不存在 → {backup_file}')
        return False

    print(f'正在从 {backup_file} 恢复...')
    
    # 确定备份类型
    if backup_file.endswith('.db'):
        # 恢复数据库
        os.makedirs(os.path.join(BASE_DIR, 'instance'), exist_ok=True)
        dst = os.path.join(BASE_DIR, 'instance', 'team.db')
        shutil.copy2(backup_file, dst)
        print(f'[OK] 数据库已恢复 → {dst}')
        
    elif backup_file.endswith('.zip'):
        # 恢复上传文件
        uploads_dir = os.path.join(BASE_DIR, 'static', 'uploads')
        with zipfile.ZipFile(backup_file, 'r') as zf:
            zf.extractall(uploads_dir)
        print(f'[OK] 上传文件已恢复 → {uploads_dir}')
        
    else:
        print('错误：不支持的备份文件格式')
        return False
        
    print('\n恢复完成！')
    return True


def list_backups():
    if not os.path.exists(BACKUP_DIR):
        print('备份目录不存在')
        return
    
    backups = []
    for f in os.listdir(BACKUP_DIR):
        fpath = os.path.join(BACKUP_DIR, f)
        if os.path.isfile(fpath):
            mtime = datetime.fromtimestamp(os.path.getmtime(fpath))
            backups.append((mtime, f, os.path.getsize(fpath)))
    
    backups.sort(reverse=True, key=lambda x: x[0])
    
    print('可用备份列表：')
    print('=' * 80)
    print(f'{"日期时间":<20} {"文件名":<40} {"大小":<10}')
    print('=' * 80)
    for mtime, fname, size in backups[:20]:
        size_str = f'{size/1024:.1f}KB' if size < 1024*1024 else f'{size/(1024*1024):.1f}MB'
        print(f'{mtime.strftime("%Y-%m-%d %H:%M"):<20} {fname:<40} {size_str:<10}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='备份/恢复工具')
    parser.add_argument('--restore', help='恢复指定的备份文件')
    parser.add_argument('--list', action='store_true', help='列出所有备份')
    args = parser.parse_args()
    
    if args.restore:
        restore(args.restore)
    elif args.list:
        list_backups()
    else:
        backup()