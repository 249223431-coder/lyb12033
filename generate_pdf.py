#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import os
import sys
import markdown
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import inch
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle
from reportlab.lib import colors
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont

def register_fonts():
    font_paths = [
        'C:\\Windows\\Fonts\\simsun.ttc',
        'C:\\Windows\\Fonts\\simhei.ttf',
        'C:\\Windows\\Fonts\\msyh.ttc',
        'C:\\Windows\\Fonts\\arial.ttf',
        '/usr/share/fonts/truetype/wqy/wqy-microhei.ttc'
    ]
    
    for font_path in font_paths:
        if os.path.exists(font_path):
            if 'simsun' in font_path.lower():
                pdfmetrics.registerFont(TTFont('SimSun', font_path))
            elif 'simhei' in font_path.lower():
                pdfmetrics.registerFont(TTFont('SimHei', font_path))
            elif 'msyh' in font_path.lower():
                pdfmetrics.registerFont(TTFont('MicrosoftYaHei', font_path))
            elif 'arial' in font_path.lower():
                pdfmetrics.registerFont(TTFont('Arial', font_path))
            elif 'wqy' in font_path.lower():
                pdfmetrics.registerFont(TTFont('WenQuanYi', font_path))

def parse_markdown(md_content):
    lines = md_content.split('\n')
    elements = []
    styles = getSampleStyleSheet()
    
    body_font = 'SimSun' if 'SimSun' in pdfmetrics.getRegisteredFontNames() else 'Arial'
    
    custom_styles = {
        'Heading1': ParagraphStyle(
            'Heading1',
            parent=styles['Heading1'],
            fontSize=18,
            alignment=1,
            spaceAfter=20,
            fontName=body_font
        ),
        'Heading2': ParagraphStyle(
            'Heading2',
            parent=styles['Heading2'],
            fontSize=14,
            spaceBefore=15,
            spaceAfter=10,
            fontName=body_font
        ),
        'Heading3': ParagraphStyle(
            'Heading3',
            parent=styles['Heading3'],
            fontSize=12,
            spaceBefore=10,
            fontName=body_font
        ),
        'BodyText': ParagraphStyle(
            'BodyText',
            parent=styles['BodyText'],
            fontSize=11,
            leading=18,
            fontName=body_font
        ),
        'Code': ParagraphStyle(
            'Code',
            parent=styles['Code'],
            fontSize=10,
            fontName='Courier',
            backColor=colors.lightgrey,
            padding=5
        )
    }
    
    i = 0
    while i < len(lines):
        line = lines[i]
        
        if line.startswith('# '):
            elements.append(Paragraph(line[2:], custom_styles['Heading1']))
            elements.append(Paragraph('---', custom_styles['BodyText']))
        
        elif line.startswith('## '):
            elements.append(Paragraph(line[3:], custom_styles['Heading2']))
        
        elif line.startswith('### '):
            elements.append(Paragraph(line[4:], custom_styles['Heading3']))
        
        elif line.startswith('#### '):
            elements.append(Paragraph(line[5:], custom_styles['Heading3']))
        
        elif line.startswith('|') and i+1 < len(lines) and lines[i+1].startswith('|'):
            table_data = []
            while i < len(lines) and lines[i].startswith('|'):
                row = [cell.strip() for cell in lines[i].split('|')[1:-1]]
                table_data.append(row)
                i += 1
            
            if table_data:
                t = Table(table_data)
                t.setStyle(TableStyle([
                    ('BACKGROUND', (0,0), (-1,0), colors.grey),
                    ('TEXTCOLOR', (0,0), (-1,0), colors.whitesmoke),
                    ('ALIGN', (0,0), (-1,-1), 'LEFT'),
                    ('FONTNAME', (0,0), (-1,-1), body_font),
                    ('FONTSIZE', (0,0), (-1,-1), 10),
                    ('BOTTOMPADDING', (0,0), (-1,0), 6),
                    ('BACKGROUND', (0,1), (-1,-1), colors.beige),
                    ('GRID', (0,0), (-1,-1), 1, colors.black)
                ]))
                elements.append(t)
                elements.append(Spacer(1, 10))
            continue
        
        elif line.startswith('**') and line.endswith('**'):
            elements.append(Paragraph(f'<b>{line[2:-2]}</b>', custom_styles['BodyText']))
        
        elif line.startswith('`') and line.endswith('`'):
            elements.append(Paragraph(f'<code>{line[1:-1]}</code>', custom_styles['Code']))
        
        elif line.startswith('```'):
            code_block = []
            i += 1
            while i < len(lines) and not lines[i].startswith('```'):
                code_block.append(lines[i])
                i += 1
            if code_block:
                code_text = '\n'.join(code_block)
                elements.append(Paragraph(f'<code>{code_text}</code>', custom_styles['Code']))
                elements.append(Spacer(1, 5))
            continue
        
        elif line.startswith('- ') or line.startswith('* '):
            elements.append(Paragraph(f'• {line[2:]}', custom_styles['BodyText']))
        
        elif line.strip():
            elements.append(Paragraph(line, custom_styles['BodyText']))
        
        elif elements and not isinstance(elements[-1], Spacer):
            elements.append(Spacer(1, 5))
        
        i += 1
    
    return elements

def generate_pdf(md_path, pdf_path):
    register_fonts()
    
    with open(md_path, 'r', encoding='utf-8') as f:
        md_content = f.read()
    
    elements = parse_markdown(md_content)
    
    doc = SimpleDocTemplate(
        pdf_path,
        pagesize=A4,
        leftMargin=inch,
        rightMargin=inch,
        topMargin=inch,
        bottomMargin=inch
    )
    
    doc.build(elements)
    print(f"PDF生成成功: {pdf_path}")

if __name__ == '__main__':
    md_file = r'e:\team\.trae\documents\user_manual.md'
    pdf_file = r'e:\team\.trae\documents\user_manual.pdf'
    
    if not os.path.exists(md_file):
        print(f"错误: 找不到文件 {md_file}")
        sys.exit(1)
    
    generate_pdf(md_file, pdf_file)
