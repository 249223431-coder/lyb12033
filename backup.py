import os
import shutil
import zipfile
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


if __name__ == '__main__':
    backup()
