import random
from flask import Blueprint, render_template, jsonify
from flask_login import login_required, current_user
from models import db, PlcData, AnomalyReport
from config import Config
from datetime import datetime, timedelta

dashboard_bp = Blueprint('dashboard', __name__)

PLC_TAGS_CONFIG = [
    {'name': 'transformer_temp', 'label': '变压器温度', 'unit': '°C', 'min_val': 20, 'max_val': 120, 'warn': 85, 'critical': 100, 'icon': 'bi-lightning-charge'},
    {'name': 'mcc_temp', 'label': 'MCC温度', 'unit': '°C', 'min_val': 20, 'max_val': 80, 'warn': 60, 'critical': 70, 'icon': 'bi-cpu'},
    {'name': 'room_temp', 'label': '配电室温度', 'unit': '°C', 'min_val': 0, 'max_val': 50, 'warn': 35, 'critical': 40, 'icon': 'bi-thermometer-half'},
    {'name': 'room_humidity', 'label': '配电室湿度', 'unit': '%RH', 'min_val': 0, 'max_val': 100, 'warn': 75, 'critical': 85, 'icon': 'bi-droplet'},
]

ALARM_TAGS_CONFIG = [
    {'name': 'main_fault', 'label': '主回路故障', 'icon': 'bi-exclamation-triangle-fill', 'db': 1, 'start': 10, 'bit': 0},
    {'name': 'overload_alarm', 'label': '过载报警', 'icon': 'bi-lightning-fill', 'db': 1, 'start': 10, 'bit': 1},
    {'name': 'temp_high', 'label': '超温报警', 'icon': 'bi-thermometer-high', 'db': 1, 'start': 10, 'bit': 2},
    {'name': 'comm_fault', 'label': '通讯故障', 'icon': 'bi-wifi-off', 'db': 1, 'start': 10, 'bit': 3},
    {'name': 'emergency_stop', 'label': '急停信号', 'icon': 'bi-stop-circle-fill', 'db': 1, 'start': 10, 'bit': 4},
    {'name': 'phase_loss', 'label': '缺相报警', 'icon': 'bi-slash-circle-fill', 'db': 1, 'start': 10, 'bit': 5},
    {'name': 'earth_fault', 'label': '接地故障', 'icon': 'bi-shield-exclamation', 'db': 1, 'start': 10, 'bit': 6},
    {'name': 'fan_fault', 'label': '风机故障', 'icon': 'bi-fan', 'db': 1, 'start': 10, 'bit': 7},
]


def _read_plc_tags():
    if not Config.PLC_ENABLED:
        return None
    try:
        import snap7
        from snap7.util import get_real, get_bool
        client = snap7.client.Client()
        client.connect(Config.PLC_HOST, Config.PLC_RACK, Config.PLC_SLOT)
        data = {}
        for tag in PLC_TAGS_CONFIG:
            cfg = Config.PLC_TAGS.get(tag['name'], {})
            value = client.db_read(cfg.get('db', 1), cfg.get('start', 0), 4)
            data[tag['name']] = round(get_real(value, 0), 1)
        client.disconnect()
        return data
    except Exception:
        return None


def _read_plc_alarms():
    if not Config.PLC_ENABLED:
        return None
    try:
        import snap7
        from snap7.util import get_bool
        client = snap7.client.Client()
        client.connect(Config.PLC_HOST, Config.PLC_RACK, Config.PLC_SLOT)
        alarms = []
        for alarm in ALARM_TAGS_CONFIG:
            value = client.db_read(alarm['db'], alarm['start'], 1)
            triggered = get_bool(value, 0, alarm['bit'])
            alarms.append({'name': alarm['name'], 'label': alarm['label'], 'icon': alarm['icon'], 'triggered': triggered})
        client.disconnect()
        return alarms
    except Exception:
        return None


def _get_plc_status():
    if not Config.PLC_ENABLED:
        return 'disabled'
    try:
        import snap7
        client = snap7.client.Client()
        client.connect(Config.PLC_HOST, Config.PLC_RACK, Config.PLC_SLOT)
        status = client.get_cpu_state()
        client.disconnect()
        return 'connected' if status == 'S7CpuStatusRun' else 'stopped'
    except Exception:
        return 'disconnected'


def _simulate_tags():
    data = {}
    for tag in PLC_TAGS_CONFIG:
        existing = PlcData.query.filter_by(tag_name=tag['name']).first()
        if existing:
            variation = random.uniform(-1.5, 1.5)
            new_val = existing.value + variation
            new_val = max(tag['min_val'], min(tag['max_val'], new_val))
            data[tag['name']] = round(new_val, 1)
        else:
            data[tag['name']] = round(random.uniform(tag['min_val'] + 10, tag['warn'] - 5), 1)
    return data


def _simulate_alarms():
    alarms = []
    for alarm in ALARM_TAGS_CONFIG:
        alarms.append({'name': alarm['name'], 'label': alarm['label'], 'icon': alarm['icon'], 'triggered': False})
    alarms[0]['triggered'] = random.random() < 0.05
    alarms[2]['triggered'] = random.random() < 0.08
    return alarms


def _update_plc_data():
    plc_data = _read_plc_tags()
    if plc_data is None:
        plc_data = _simulate_tags()

    for tag in PLC_TAGS_CONFIG:
        tag_name = tag['name']
        if tag_name in plc_data:
            existing = PlcData.query.filter_by(tag_name=tag_name).first()
            if existing:
                existing.value = plc_data[tag_name]
                existing.updated_at = datetime.utcnow()
            else:
                db.session.add(PlcData(
                    tag_name=tag_name, value=plc_data[tag_name],
                    unit=tag['unit'], updated_at=datetime.utcnow()
                ))
    db.session.commit()


def _get_status(val, tag):
    if val >= tag['critical']:
        return 'critical'
    elif val >= tag['warn']:
        return 'warning'
    return 'normal'


def _get_alarm_data():
    if Config.PLC_ENABLED:
        alarms = _read_plc_alarms()
    if not Config.PLC_ENABLED or alarms is None:
        alarms = _simulate_alarms()

    result = []
    for alarm in alarms:
        plc_existing = PlcData.query.filter_by(tag_name=alarm['name']).first()
        if plc_existing is not None:
            alarm['triggered'] = plc_existing.value >= 1.0
            plc_existing.value = 1.0 if alarm['triggered'] else 0.0
            plc_existing.updated_at = datetime.utcnow()
        else:
            db.session.add(PlcData(
                tag_name=alarm['name'], value=1.0 if alarm['triggered'] else 0.0,
                unit='', updated_at=datetime.utcnow()
            ))
        result.append(alarm)

    db.session.commit()
    return result


@dashboard_bp.route('/')
@login_required
def index():
    _update_plc_data()
    plc_status = _get_plc_status()
    tags_data = []
    has_alarm = False
    has_warning = False

    for tag in PLC_TAGS_CONFIG:
        entry = PlcData.query.filter_by(tag_name=tag['name']).first()
        val = entry.value if entry else 0
        status = _get_status(val, tag)
        if status == 'critical':
            has_alarm = True
        elif status == 'warning':
            has_warning = True
        pct = min(100, max(0, (val - tag['min_val']) / (tag['max_val'] - tag['min_val']) * 100))
        tags_data.append({
            'name': tag['name'], 'label': tag['label'], 'value': val,
            'unit': tag['unit'], 'warn': tag['warn'], 'critical': tag['critical'],
            'status': status, 'pct': round(pct, 1), 'icon': tag['icon'],
            'updated_at': entry.updated_at.strftime('%H:%M:%S') if entry and entry.updated_at else '--:--:--',
        })

    alarm_data = _get_alarm_data()
    active_alarms = [a for a in alarm_data if a['triggered']]

    overall_status = 'critical' if has_alarm else ('warning' if has_warning else 'normal')

    return render_template('dashboard/index.html',
                           tags_data=tags_data,
                           alarm_data=alarm_data,
                           active_alarms=active_alarms,
                           plc_status=plc_status,
                           plc_enabled=Config.PLC_ENABLED,
                           plc_host=Config.PLC_HOST,
                           overall_status=overall_status)


@dashboard_bp.route('/api/data')
@login_required
def api_data():
    _update_plc_data()
    result = []
    for tag in PLC_TAGS_CONFIG:
        entry = PlcData.query.filter_by(tag_name=tag['name']).first()
        val = entry.value if entry else 0
        status = _get_status(val, tag)
        pct = min(100, max(0, (val - tag['min_val']) / (tag['max_val'] - tag['min_val']) * 100))
        result.append({
            'name': tag['name'], 'label': tag['label'], 'value': val,
            'unit': tag['unit'], 'status': status, 'pct': round(pct, 1),
            'warn': tag['warn'], 'critical': tag['critical'],
            'updated_at': entry.updated_at.strftime('%H:%M:%S') if entry and entry.updated_at else '--:--:--',
        })

    alarms = _get_alarm_data()
    active = [a for a in alarms if a['triggered']]

    plc_status = _get_plc_status()

    return jsonify({
        'tags': result,
        'alarms': alarms,
        'active_alarms': active,
        'plc_status': plc_status,
        'timestamp': datetime.utcnow().strftime('%Y-%m-%d %H:%M:%S'),
    })
