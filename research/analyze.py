"""Validate and plot the frozen experiment. Usage: python research/analyze.py INPUT OUTPUT."""
import csv
import json
import re
from pathlib import Path
import sys

local = Path(__file__).resolve().parents[1] / 'target' / 'research-python'
if local.exists():
    sys.path.insert(0, str(local))
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.ticker import FixedLocator, FuncFormatter, NullFormatter

source, output = map(Path, sys.argv[1:3])
output.mkdir(parents=True, exist_ok=True)
with (source / 'samples.csv').open(encoding='utf-8-sig', newline='') as f:
    rows = list(csv.DictReader(f))
assert (source / 'COMPLETE.txt').exists(), 'Incomplete experiment'
assert all(r['Correct'].lower() == 'true' for r in rows)
datasets = list(dict.fromkeys(r['Dataset'] for r in rows))
configs = list(dict.fromkeys(r['Configuration'] for r in rows))
fixed = ['Sequential DFS'] + [f'Fixed / {n} threads' for n in (1, 2, 4, 8)]
groups = {}
summary = []
for dataset in datasets:
    hashes = {r['PathSetSHA256'] for r in rows if r['Dataset'] == dataset}
    assert len(hashes) == 1, dataset
    for config in configs:
        all_rows = [r for r in rows if r['Dataset'] == dataset and r['Configuration'] == config]
        if not all_rows:
            continue
        measured = [r for r in all_rows if r['Warmup'].lower() == 'false']
        assert len(measured) == 10 and len(all_rows) == 13, (dataset, config)
        assert sorted(int(r['Round']) for r in measured) == list(range(1, 11))
        measured.sort(key=lambda r: int(r['Round']))
        groups[dataset, config] = measured
        values = np.array([float(r['TotalMs']) for r in measured])
        record = dict(Dataset=dataset, Configuration=config, MedianMs=float(np.median(values)),
                      MinMs=float(values.min()), MaxMs=float(values.max()), StdDevMs=float(values.std()))
        for field in ('EngineMs', 'CpuMs', 'SampledPeakWorkingSetBytes', 'ResourceSamplingMs',
                      'SamplingMs', 'DecisionMs', 'PoolStartupMs', 'SchedulingMs'):
            entries = [float(r[field]) for r in measured if r[field] != '']
            record[field] = float(np.median(entries)) if entries else None
        record['SelectedStrategies'] = '; '.join(sorted({r['SelectedStrategy'] for r in measured if r['SelectedStrategy']}))
        summary.append(record)
with (output / 'summary.csv').open('w', newline='', encoding='utf-8') as f:
    writer = csv.DictWriter(f, fieldnames=list(summary[0])); writer.writeheader(); writer.writerows(summary)
rng = np.random.default_rng(20261023)
comparisons = []
for dataset in datasets:
    records = [r for r in summary if r['Dataset'] == dataset]
    best = min((r for r in records if r['Configuration'] in fixed), key=lambda r: r['MedianMs'])
    hybrid = next(r for r in records if r['Configuration'] == 'Hybrid adaptive')
    a = np.array([float(r['TotalMs']) for r in groups[dataset, 'Hybrid adaptive']])
    b = np.array([float(r['TotalMs']) for r in groups[dataset, best['Configuration']]])
    indices = rng.integers(0, 10, (10000, 10))
    ratios = np.median(a[indices], axis=1) / np.median(b[indices], axis=1)
    lo, hi = np.quantile(ratios, [.025, .975])
    old = next(r for r in records if r['Configuration'] == 'October-22 adaptive')
    comparisons.append(dict(Dataset=dataset, BestFixed=best['Configuration'], BestFixedMs=best['MedianMs'],
                            HybridMs=hybrid['MedianMs'], Ratio=hybrid['MedianMs']/best['MedianMs'],
                            Low95=float(lo), High95=float(hi), LegacyMs=old['MedianMs'],
                            HybridVsLegacy=hybrid['MedianMs']/old['MedianMs'], Strategy=hybrid['SelectedStrategies']))
with (output / 'comparison.csv').open('w', newline='', encoding='utf-8') as f:
    writer = csv.DictWriter(f, fieldnames=list(comparisons[0])); writer.writeheader(); writer.writerows(comparisons)
plt.rcParams.update({'font.size': 9, 'axes.spines.top': False, 'axes.spines.right': False})
def save(fig, name):
    fig.savefig(output / (name+'.png'), dpi=180, bbox_inches='tight')
    fig.savefig(output / (name+'.svg'), bbox_inches='tight'); plt.close(fig)
fig, axes = plt.subplots(3, 3, figsize=(15, 12))
order = fixed + ['October-22 adaptive', 'Hybrid adaptive', 'PowerShell']
labels = ['DFS', 'Fixed 1', 'Fixed 2', 'Fixed 4', 'Fixed 8', 'Old adaptive', 'Hybrid', 'PowerShell']
for ax, dataset in zip(axes.flat, datasets):
    records = {r['Configuration']:r for r in summary if r['Dataset'] == dataset}
    names = [n for n in order if n in records]
    values = [records[n]['MedianMs'] for n in names]
    for position, name in enumerate(names):
        record=records[name]; median=record['MedianMs']
        ax.errorbar(median,position,xerr=[[median-record['MinMs']],[record['MaxMs']-median]],
                    fmt='o',capsize=3,color='#008c87' if name=='Hybrid adaptive' else '#697c95')
    ax.set_yticks(range(len(names)),[labels[order.index(n)] for n in names])
    ax.set_xscale('log')
    ticks=np.geomspace(min(records[n]['MinMs'] for n in names)*.9,max(records[n]['MaxMs'] for n in names)*1.1,4)
    ax.xaxis.set_major_locator(FixedLocator(ticks));ax.xaxis.set_major_formatter(FuncFormatter(lambda value,pos:f'{value:.0f}'))
    ax.xaxis.set_minor_formatter(NullFormatter())
    ax.set_xlabel('Process elapsed ms (log scale); median and min–max'); ax.set_title(dataset)
fig.suptitle('Identical queries: 3 warmups + 10 measured process runs per configuration'); fig.tight_layout()
save(fig, 'algorithm-comparison')
fig, ax = plt.subplots(figsize=(10, 5))
x = np.arange(len(comparisons)); ratios = np.array([r['Ratio'] for r in comparisons])
ax.errorbar(x, ratios, yerr=[np.maximum(0,ratios-[r['Low95'] for r in comparisons]),
                            np.maximum(0,np.array([r['High95'] for r in comparisons])-ratios)],fmt='o',capsize=4,color='#008c87')
ax.axhline(1,color='black',lw=1); ax.set_xticks(x, datasets, rotation=35, ha='right')
ax.set_ylabel('Hybrid / fastest fixed median time (lower is better)')
ax.set_title('Paired-round bootstrap 95% intervals; fixed baseline selected on these observations')
save(fig, 'fastest-fixed')
fig, axes = plt.subplots(1, 3, figsize=(16, 5))
for config in order:
    records = [r for r in summary if r['Configuration'] == config]
    if not records: continue
    for ax, field, label in zip(axes, ['CpuMs','SampledPeakWorkingSetBytes','MedianMs'],
                               ['CPU milliseconds','Sampled peak working set (MiB)','Process elapsed milliseconds']):
        ys = [r[field]/(1048576 if field=='SampledPeakWorkingSetBytes' else 1) for r in records]
        ax.plot([datasets.index(r['Dataset']) for r in records],ys,marker='.',label=config)
        ax.set_xticks(range(len(datasets)),datasets,rotation=70,ha='right'); ax.set_ylabel(label)
axes[2].set_yscale('log'); axes[0].legend(fontsize=7); fig.tight_layout(); save(fig,'resource-tradeoffs')
hybrids = [next(r for r in summary if r['Dataset']==d and r['Configuration']=='Hybrid adaptive') for d in datasets]
fig, ax = plt.subplots(figsize=(11,5)); bottom=np.zeros(len(datasets))
for field,label in [('SamplingMs','Profile'),('DecisionMs','Policy'),('PoolStartupMs','Pool startup'),('SchedulingMs','Scheduling')]:
    vals=np.array([r[field] or 0 for r in hybrids]); ax.bar(x,vals,bottom=bottom,label=label);bottom+=vals
ax.set_xticks(x,datasets,rotation=35,ha='right');ax.set_ylabel('Component median milliseconds');ax.legend();
ax.set_title('Measured hybrid overhead components (sum of medians; included in elapsed time)');save(fig,'overhead')
with (source/'telemetry.csv').open(encoding='utf-8-sig',newline='') as f: telemetry=list(csv.DictReader(f))
with (source/'decisions.csv').open(encoding='utf-8-sig',newline='') as f: decisions=list(csv.DictReader(f))
fig, axes=plt.subplots(1,3,figsize=(15,4))
for ax,dataset in zip(axes,['Deep-20000','Wide-20000','Mixed-20000']):
    traces=[r for r in telemetry if r['Dataset']==dataset and r['Configuration']=='Hybrid adaptive' and r['Warmup'].lower()=='false' and r['Round']=='1']
    ax.scatter([float(r['EngineElapsedMs']) for r in traces],[int(r['ActiveWorkers']) for r in traces],s=20,label='Observed active tasks')
    changes=[r for r in decisions if r['Dataset']==dataset and r['Configuration']=='Hybrid adaptive' and r['Warmup'].lower()=='false' and r['Round']=='1' and r['Event']=='Workers']
    times=[float(re.search(r't=([0-9.]+)ms',r['Reason']).group(1)) for r in changes]
    budgets=[int(r['New']) for r in changes]
    if traces:
        times.append(max(float(r['EngineElapsedMs']) for r in traces));budgets.append(budgets[-1])
    ax.step(times,budgets,where='post',label='Selected worker budget',color='#008c87',alpha=.7)
    ax.set_title(dataset);ax.set_xlabel('Engine elapsed ms');ax.set_ylabel('Active workers');ax.set_ylim(0,9);ax.legend(fontsize=7)
fig.suptitle('Observed worker events, measured round 1 (event samples, not continuous utilization)');fig.tight_layout();save(fig,'runtime-workers')
audit=dict(invocations=len(rows), measured=sum(r['Warmup'].lower()=='false' for r in rows),
           warmups=sum(r['Warmup'].lower()=='true' for r in rows), correct=True,
           datasets=len(datasets), comparisons=comparisons,
           hybrid_policy_events=sum(r['Configuration']=='Hybrid adaptive' for r in decisions))
(output/'audit.json').write_text(json.dumps(audit,indent=2),encoding='utf-8')
print(json.dumps(audit,indent=2))
