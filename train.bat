@echo off
echo ====================================================
echo Starting ML-Agents Training: Roomba Cleaner
echo ====================================================
call .venv\Scripts\activate.bat
mlagents-learn config\roomba_ppo.yaml --run-id=Roomba_Pro_v1 --resume
pause
