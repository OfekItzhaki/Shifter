@echo off
echo Starting Jobuler Solver Service...
cd /d "%~dp0..\..\apps\solver"
pip install -r requirements.txt -q
python -m uvicorn main:app --reload --port 8000
