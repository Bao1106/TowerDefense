# SDD ledger — plan: docs/superpowers/plans/2026-10-09-morale-cleanup.md

> Reconstructed 2026-10-09 from the session transcript: the workspace `.superpowers/sdd/2026-10-09-morale-cleanup/` was deleted (12:01) before its ledger was copied. Lines are the ones written to it, in order.

Spec: docs/superpowers/specs/2026-10-06-morale-load-model-design.md (§5.2, §5.5, §7.3) + ledger minors (docs/superpowers/measurements/raw/2026-10-06-load-model/ledger.md).
Setup: Ruling: branch is feature/refactor-morale-system (user's choice, already checked out at b914ef92 = develop tip), not the plan's fix/morale-cleanup — cost if wrong: a branch rename.
Setup: Ruling: every "Stage" step stops for the user's own commit before the next task (standing user rule "stage file thôi … commit mình sẽ tự làm" overrides continuous execution) — cost if wrong: a pause per task.

Pre-flight:
- T2/T3/T4 all edit TDEnemyView.cs (T2: blow guards; T3: m_SpawnId in Initialize; T4: m_AuraVisual in Awake/Initialize/Die) — disjoint members, no interface clash.
- T3/T4/T6 all edit TDBalanceValidator.Run(): T4 loads enemies, T6 loads levels — each LoadConfig once, before loops (Global Constraints). No clash.
- T1/T6 both edit TDPressureProbe.cs (T1: Stint/RecordStint/Build/End; T6: AuraRateAt comment) — disjoint.
- No task consumes another task's Produces block.
Setup: Ruling: task-done's test command re-reads the validator result file the MCP run writes into this workspace (validators run inside Unity, not a shell); task-done runs after the user's commit — cost if wrong: the ledger proves the file says PASS, not that a shell ran Unity.
Task 1: Ruling: A4 thresholds — "near full HP" = hpFrac ≥ 0.8, "red stress" = band Stressed; spec §7.3 gives no number (plan's call) — cost if wrong: one constant (A4_MIN_HP_FRAC).
Task 1: Ruling: Play Mode step used Morale.AddSpike(80 − value) instead of the plan's Morale.SetValue(80) — SetValue is private; AddSpike writes through it — cost if wrong: none (test-only call).
Task 1: RED = CS0117 (Stint.retreated/hpFrac/band, CountA4 missing) → GREEN validators PASS 0/0. Play Mode: (c) one "Pressure report — GameOver", A4 1, Knight "retreat hp 100 % Stressed", Defender "death/end hp 100 % Calm"; (d) after RetryGameplay + wave 1 → new report, stints 0, A4 0.
Task 1: note: a stint's stressGained misses a rise made in the same frame as the retreat (sampled after the −70) — test artefact (spike + retreat in one call); in play stress is sampled every frame. Not a finding.
Task 1: staged (3 files), waiting for the user's commit.
Task 1: complete (commits b914ef9..2180531, tests: grep -c 'PASS — 0 failures' .superpowers/sdd/2026-10-09-morale-cleanup/validator-task-1.txt → 2)
Task 2: Ruling: added two shared reflection helpers to TDMoraleValidator (internal SetField/GetField<T>; missing field → "[REFLECT] Type.field missing" failure) instead of per-test inline reflection — Tasks 3–4 reuse them from TDBalanceValidator — cost if wrong: one small coupling between the two validators.
Task 2: RED = exactly [ZERO_DAMAGE_NOT_A_HIT] (Balance PASS) → GREEN both PASS 0/0. Guard in TDOperatorView.TakeDamage (damage <= 0) covers both enemy call sites; TDEnemyView also skips the swing (blocked branch + passing blow) when m_AttackDamage <= 0.
Task 2: staged (3 files), waiting for the user's commit.
Task 2: complete (commits 2180531..e553b0b, tests: grep -c 'PASS — 0 failures' .superpowers/sdd/2026-10-09-morale-cleanup/validator-task-2.txt → 2)
Task 3: Ruling: split the plan's RED in two — after the [REFLECT] RED (fields missing, as planned), added SpawnId/m_PendingSpawnId WITHOUT the guard and re-ran: [HIT_SKIPS_REUSED_TARGET] (hp 90) + [HIT_CONTROL] (hp 80, cumulative) — proves the test catches the real bug, not just a missing field — cost if wrong: none.
Task 3: GREEN both PASS 0/0. Guard: ExecuteHit drops the shot (and its splash) when m_PendingTarget.SpawnId != m_PendingSpawnId; s_NextSpawnId is a static counter bumped in TDEnemyView.Initialize.
Task 3: note: a target that reached the gate and went back to the pool (not reused yet) keeps its SpawnId, so a Moon splash can still centre on its pooled position — pre-existing, outside this finding (reuse). Not acted on.
Task 3: staged (3 files), waiting for the user's commit.
Task 3: complete (commits e553b0b..163f8eb, tests: grep -c 'PASS — 0 failures' .superpowers/sdd/2026-10-09-morale-cleanup/validator-task-3.txt → 2)
Task 4: RED = [REFLECT] TDEnemyView.m_AuraVisual missing → field added: [HERALD_AURA_WIRED] unset (as planned) → prefab line added: GREEN both PASS 0/0.
Task 4: Play Mode (DEMO-1): Herald spawn 4 ring True → TakeDamage(1e6) → ring False same call, gone from registry; after 3 s a new Herald = same instance (-40026, pool reuse), spawn 9, ring True. Errors: only the MCP plugin's "path contains spaces" notice; no exceptions.
Task 4: staged (TDEnemyView.cs, TDBalanceValidator.cs, Herald.prefab), waiting for the user's commit.
Task 4: incident (fixed): the Edit tool dropped the trailing spaces of "m_Name: " / "m_EditorClassIdentifier: " in Herald.prefab, and a Git-Bash `sed -i` with `\r` in the pattern did not match (msys sed reads CRLF files with the CR stripped) and rewrote the working copy as LF. Restored the spaces + `unix2dos`; staged prefab diff is now the single m_AuraVisual line; validators re-run after refresh: PASS 0/0.
Task 4: complete (commits 163f8eb..3c4f5a6, tests: grep -c 'PASS — 0 failures' .superpowers/sdd/2026-10-09-morale-cleanup/validator-task-4.txt → 2)
Task 5: RED 1 = CS0117 'LogLeaks' (as planned); RED 2 = flag added but not wired → [LEAK_LOG_GATED] 1 line with LogLeaks off; GREEN both PASS 0/0. No code reads "[Leak]" lines (grep: only the producer and the test).
Task 5: Ruling: kept the plan's shape as an early-return branch — with LogLeaks off ReceiveLeak is called and nothing else is built (no lists, no strings); the logging branch is unchanged — cost if wrong: two ReceiveLeak call sites to keep in step.
Task 5: staged (2 files), waiting for the user's commit.
Task 5: complete (commits 3c4f5a6..e50cac2, tests: grep -c 'PASS — 0 failures' .superpowers/sdd/2026-10-09-morale-cleanup/validator-task-5.txt → 2)
Task 6: Ruling: Growth coverage widening has no RED by nature (it extends a test over existing code) — evidence instead: the growths the validator now checks are 1, 2.7, 3.5 (read once from TDLevelConfigSettings in Run), GROWTH_* PASS at 3.5, full run ~0.95 s — cost if wrong: none; a future level growth is covered automatically.
Task 6: STRESS_AURA_HERALD removed (grep empty); 3 comments fixed (TDOperatorMorale:18, TDPressureProbe ponytail note, TDLevelConfigSettings ×1.5); m_Ratio field + its write moved under #if UNITY_EDITOR. Player compile (Android, CompilePlayerScripts + assemblyCompilationFinished hook): 24 assemblies, 0 errors, 0 m_Ratio/CS0414 lines; the hook does catch warnings (it reports a pre-existing CS0162 in TDMoraleDebugOverlay.cs:34). Validators PASS 0/0 (validator-task-6.txt).
Task 6: note: pre-existing CS0162 "unreachable code" at TDMoraleDebugOverlay.cs:34 in player builds — outside this plan, not acted on.
Task 6: staged (6 files), waiting for the user's commit.
Task 6: complete (commits e50cac2..7f52283, tests: grep -c 'PASS — 0 failures' .superpowers/sdd/2026-10-09-morale-cleanup/validator-task-6.txt → 2)
Final review: opus reviewer (fresh context) over b914ef92..7f52283d — 0 Critical, 0 Important, 4 Minor; all 5 Review Focus items hold; ledger rulings judged sound. Verdict: Ready to merge — Yes. No fix pass needed.
Final: minor (deferred): TDConstant.cs:394 comment "Radius lives on the enemy data" is stale (radius is hardcoded `dist <= 3` in TDPressureProbe.AuraRateAt) — one-line comment fix.
Final: minor (deferred): TDAttackVFX (turret bolts) has the same reused-target bug Task 3 fixed for operators — a bolt in flight when its target reaches the gate can home onto that pooled object's next spawn across the map; ~3 lines with the new SpawnId.
Final: minor (deferred): DoRetreat samples stress only after the −70, so a rise earlier in the same frame is lost from the stint's stressGained (r measurement noise; A4 unaffected) — add SampleStress() beside the band capture.
Final: minor (deferred): the validators touch live static state (ReportLeak → TDPressureProbe.RecordLeak +2; OperatorImpacted fires into any still-subscribed TDEffectManager) — run them in Edit Mode only / say so in the menu tooltip.
Final: Ruling: A4 ignores stint length — a near-full-HP Stressed retreat is stress-driven however short the stay; spec §7.3 gives no duration and the −70 / 8 s price makes instant redeploy-retreat rare — cost if wrong: a slightly inflated A4 count.
Final: Ruling: retreats before wave 1 of a retried match land in the old stint list and are wiped at OnWaveStarted(0) — pre-existing lifecycle; A4 unaffected (roster rebuilt, everyone Calm) — cost if wrong: a pre-wave stint missing from a report.
Final: Ruling: a target that reached the gate but is not yet reused still takes the shot (splash at the gate) — that is where the shot was aimed — cost if wrong: a stray splash near the end gate.
Final: Ruling: the m_AttackDamage > 0 check in the blocked branch never matters for the Herald (baseAttackSpeed 0) — kept, harmless and matches "no damage, no swing" — cost if wrong: none.
Final: Ruling: HIT_SKIPS_REUSED_TARGET does not drive the splash branch — the guard sits before the split; a splash test needs grid + registry — cost if wrong: a refactor moving the guard below the split would go untested.
Final: Ruling: LoadConfig runs in more than one check per Run() — each call is outside every loop (the constraint is per call site) — cost if wrong: a few ms per validator run.
Final: Ruling: commits carry no Co-Authored-By trailer — the user makes the commits (standing rule), so the trailer is theirs to include — cost if wrong: attribution only.
Final: Ruling: CS0162 at TDMoraleDebugOverlay.cs:34 is pre-existing and outside this plan — cost if wrong: one player-build warning.
Final: workspace gone before the copy (deleted 12:01, not by this run); ledger reconstructed here from the transcript. The validator result files and the review diff it held are not recoverable — the six commits and the review verdict above are the record.
Post-merge (2026-10-09): fixed deferred minor TDAttackVFX reused target — bolt stores the target's SpawnId at Init and lets go (finishes at the last known position) when it changes; test ReusedBolt: RED [REFLECT] m_TargetSpawnId missing → field without guard: [BOLT_DROPS_REUSED_TARGET] heads for (100,0,0) → GREEN, both validators PASS 0/0. Test keeps the bolt from arriving (OnImpact would release it into the static bullet pool).
Post-merge (2026-10-09): fixed deferred minor TDConstant.cs:394 stale aura comment. Still deferred: DoRetreat stressGained sampling; validators touching live static state.
