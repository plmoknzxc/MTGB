"""Actual convoke COLORLESS choice pays generic one; no colorless mana produced."""
import importlib.util,json,subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('mixed_tap',Path(__file__).with_name('Test-MixedTapCosts.py'))
driver=importlib.util.module_from_spec(spec);spec.loader.exec_module(driver)
driver.OUT=ROOT/'.tools/convoke-generic-1007/native';driver.OUT.mkdir(parents=True,exist_ok=True)
driver.CLASSES=driver.OUT/'classes';driver.CLASSES.mkdir(exist_ok=True)
cards=['Selesnya Guildmage','Soul Warden','Llanowar Elves','Savannah Lions']
driver.CASES['convoke-generic']=dict(source='March of the Multitudes',hand=['March of the Multitudes'],field=cards,
    enemy=[],grave=[],purpose='convoke',x=1,generic=True,wanted=cards)

if __name__=='__main__':
    subprocess.run(['javac','-encoding','UTF-8','-cp',str(driver.RT/'classes')+';'+str(driver.RT/'lib/*'),'-d',str(driver.CLASSES),str(driver.FIXTURE)],check=True)
    result=driver.run('convoke-generic')
    print(result['result'],flush=True)
