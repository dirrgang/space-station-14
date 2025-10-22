# CE‑Style Penetration & Impact Impulse System for Space Station 14

**Author:** Dennis (proposal draft)
**Date:** 22 Oct 2025 (Europe/Berlin)
**Status:** Draft for discussion

---

## 1) Summary

Introduce a **deterministic, CE-style pre‑pass** for *physical* damage that compares an attack’s **Armor Penetration (AP)** against layer‑specific **Armor Ratings (AR)**. On failed penetration, sharp damage converts to **reduced blunt** (behind‑armor blunt trauma). We **reuse** SS14’s projectile travel, pass‑through, hit events, and DamageSpecifier while **replacing** coefficient‑only armor for physical interactions. The model is **AP‑only** (no kinetic‑energy budget) and inserts a species **core layer** (Head/Body) between entry and exit armor to decide lodging vs. through‑and‑through.

**Key outcomes**

* Ammo types (HP/FM﻿J/SP/AP) become meaningfully distinct and intuitive.
* Armor meaningfully blocks small‑AP threats; large‑AP threats penetrate predictably.
* Shotguns/beanbags keep value via **blunt on deflect** (optional stamina coupling).
* Systems remain modular and opt‑in; non‑physical damage continues to use existing coefficients.

---

## 2) Goals & Non‑Goals

**Goals**

* Deterministic AP↔AR checks per armor layer on **entry** and **exit**.
* Use a **single metric (AP in mm RHA)**; remove magic AP attrition and kinetic energy.
* Insert **species core layers** (`coreHeadArMm`, `coreBodyArMm`) as the last gate before exit.
* Integrate **clothing and species** as layers; reuse coverage; honor **shield arcs** and front/back where available.
* Integrate **melee** using the same AP/AR rules.
* Minimal surface area change; feature‑flagged and data‑driven.

**Non‑Goals**

* Full real‑world ballistics (yaw, fragmentation, gel tracks).
* Replacing non‑physical mitigation (Heat/Shock/etc.).
* Client prediction of ricochets (server‑authoritative first).

---

## 3) Terminology

* **AP (Armor Penetration):** numeric capacity of an attack to defeat sharp/blunt protection. For sharp, expressed in *mm RHA‑equivalent*.
* **AR (Armor Rating):** per layer resistance vs specific damage mode (Sharp/Blunt).
* **BABT:** Behind‑Armor Blunt Trauma; represented as **blunt damage created on deflect** when armor stops sharp penetration.

---

## 4) High‑Level Architecture

1. **Penetration pre‑pass (new):** runs on projectile/melee hit **before** `TryChangeDamage`. Resolves AP↔AR across outer→inner layers (entry), then **core layer** (Head/Body), then inner→outer layers (exit). If any gate fails, the projectile **lodges**.
2. **Damage application (existing):** `TryChangeDamage` receives the **transformed** `DamageSpecifier`. For **physical** types handled by the pre‑pass, armor coefficients are skipped or set to 1.0.
3. **Pass‑through (existing):** projectile travel/continuation stays unchanged and independent of penetration logic.
4. **Reflection/ricochet:** keep **magical/tech Reflect** as‑is; ballistic **ricochet** is optional and off by default.
5. **Stamina interaction (optional):** map **blunt created on deflect** to stamina (capped); no new CC system.

Feature is **opt‑in** via server config (`pen_prepass.enabled`) and/or content presence (items with `ArmorRating`).

---

## 5) Data Model (components)

### 5.1 PenetratingProjectile (new)

* `apSharpMm : float` (initial AP in **mm RHA**)
* `apBluntMm : float` (optional; for crush‑through/blunt)
* `deflectBluntScalar : float` (0–1; fraction of sharp converted to blunt on fail)
* **Ammo family hint:** `family: HP|FMJ|SP|AP` (UI only)

### 5.2 ArmorRating (new)

* `arSharpMm : float`
* `arBluntMm : float`
* `coverage : [Head|Body|TorsoFront|TorsoBack]` (front/back used only by forks with hitboxes; otherwise Head/Body)
* `stopsOnDeflect : bool` (plates/helmets true; cloth false)
* `layerOrder : enum` (Shield > HardsuitOuter > Helmet/OuterHead > OuterClothing > InnerClothing > SpeciesSurface > SpeciesSubdermal)

### 5.3 SpeciesCoreArmor (new)

* `coreHeadArMm : float` (required **core layer** for Head)
* `coreBodyArMm : float` (required **core layer** for Body)

### 5.4 KineticDissipation (optional; exosuits/rigid armor)

* `dissipationFactor : float` (0.4–1.0; multiplies **stamina** from blunt on deflect)

### 5.5 PenHandledFlags (transient)

* Marks physical types handled by the pre‑pass so coefficients are skipped during this call.

### 5.6 Content safety switch

* On any item with `ArmorRating`, set `physicalCoefficientsDisabled: true` in its existing armor mod component to prevent double mitigation.

---

## 6) Systems & Integration

### 6.1 PenetrationSystem (new)

**Hook:** subscribe to projectile/melee hit event **before** damage application.

**Steps:**

1. **Region & facing:** Resolve Head/Body via the selected resolver. Determine Front/Back for Body via facing vs shot direction. Determine **shield eligibility** by arc test.
2. **Entry stack:** Collect layers (shield → outerwear → inner → species‑surface) that cover the region (and facing). Sort by `layerOrder`.
3. **Per layer (entry):**

   * **AP gate:** if `apRemaining ≥ arLayer` → penetrate and set `apRemaining -= arLayer`; else → **deflect** (sharp→blunt via `deflectBluntScalar * (1 − ap/ar)^γ`), stop if `stopsOnDeflect`.
4. **Core layer (species):**

   * Use `coreHeadArMm` or `coreBodyArMm` as a required middle layer. Same rule (subtract or lodge on fail).
5. **Exit stack:** Process inner→outer rear layers with remaining `apRemaining`; on any fail → **lodge**, else → **exit**.
6. **Post‑armor damage & stamina:** Forward transformed `DamageSpecifier`. If stamina coupling enabled, add `stamina += bluntCreated * staminaScalar * dissipationFactor` with per‑window caps.

### 6.2 DamageableSystem integration

* If `PenHandledFlags` marks a physical type, **skip** clothing/species **physical coefficients** for this call. Also support content‑side `physicalCoefficientsDisabled: true`.

### 6.3 Projectile pass‑through (reuse)

* Continue to use the existing pass‑through/penetration threshold for travel distance; it is independent of AP resolution.

### 6.4 Reflect vs ballistic ricochet

* Keep **ReflectSystem** for energy shields/magic.
* Ballistic **ricochet** optional/off by default; if enabled, compute a small chance on failed sharp based on `(arLayer − apRemaining)` and impact angle.

---

## 7) Exit Resistance (AP‑only)

* Initialize `apRemaining = apSharpMm` (or `apBluntMm` for blunt contests).
* Entry layers subtract AR on success; failure deflects/creates blunt and may stop.
* The **core layer** (Head or Body) always occurs between entry and exit; failure lodges; success subtracts AR.
* Exit layers subtract AR on success; any failure lodges; success on all rear layers yields **exit** and the projectile may continue traveling.

---

## 8) Stamina‑Only Effects (no new CC system)

* Optionally map **blunt created on deflect** to **stamina** damage via `stamina.fromBluntScalar` (default off).
* **Caps:** enforce per‑window cap (e.g., 35 per 1.5 s). Aggregate multi‑pellet impacts in ~80 ms into a single stamina application to avoid micro‑spam.

---

## 9) Ammo Families (authoring guidance)

* **HP:** low AP, high tissue damage, **low pass‑through** threshold; high `deflectBluntScalar`.
* **FMJ:** baseline AP/damage, normal threshold.
* **Soft‑Point:** very low AP, high blunt conversion on fail, minimal pass‑through.
* **AP:** high AP, slightly reduced tissue damage, normal/high threshold.

Expose AP/AR on examine/tooltips (numbers and qualitative tags: *Stops pistols*, *Likely to penetrate soft armor*, etc.).

---

## 10) (Reserved)

Out of scope for the initial ballistics + melee implementation. Can be revisited later to apply AP logic to non‑projectile penetrations (injections/bites).

---

## 11) Configuration & Cvars

* `pen_prepass.enabled : bool`
* `pen_prepass.regionResolver = blue_noise|random|hitbox`
* `pen_prepass.p_head = 0.15`
* `pen_prepass.minQualifyingDamage = 3`
* `pen_prepass.minQualifyingMs = 80`
* `stamina.fromBluntScalar : float` (default 0 = off)
* `stamina.capPerWindow : float`, `stamina.windowSec : float`

All tunables server‑side; hot‑reload where possible.

---

## 12) Migration Plan

**Phase 1 (prototype)**

* Implement systems/components; convert: riot shields, ballistic helmets, vests, one hardsuit; 9mm FMJ/AP, 12g slug/beanbag, 5.56 FMJ/AP. Set physical coefficients to 1.0 on those items. Ship behind feature flag.
* Verify and reuse **body‑part coverage** from existing clothing/species content; if absent, add `coverage` arrays.

**Phase 2**

* Add species armor (scales/chitin). Enable exit‑side checks. Enable optional stamina‑from‑blunt coupling.

**Phase 3**

* Broaden content. Document authoring guidelines. Improve UI/telemetry.

---

## 13) Testing Strategy

* **Unit:** AP‑only outcomes (penetrate/deflect), **AP depletion** across multiple layers, core layer lodge/penetrate logic; stamina mapping caps.
* **Integration (headless):** golden tests firing representative rounds at mannequins in Light/Medium/Heavy armor; assert final `DamageSpecifier`, pass‑through behavior, Head fraction ~p_head, pellet aggregation, shield arc behavior.
* **Regression:** non‑physical coefficients unchanged; thresholds/crit/death unaffected.
* **Playtests:** shotgun vs armor feel; exosuit stability; head protection matters without RNG clumping.

---

## 14) Performance Considerations

* Few layers per hit (O(n) with n≈1–4).
* Simple float math; negligible vs ECS/physics.
* Pellet aggregation reduces event spam.
* Telemetry logging behind a debug flag.

---

## 15) UX & Telemetry

* **Combat log**: “AP 12.0→7.5 mm; 4.5 mm plate penetrated; core 5.0 mm penetrated; exit blocked by backplate 10.0 mm.”
* **Tooltips** for ammo/armor: numeric AP/AR + qualitative tags.
* **SFX/FX**: distinct feedback for deflection vs penetration.
* **Metrics**: counters/histograms per weapon family — penetration rate, deflection rate, exit rate, head fraction.

---

## 16) Risks & Mitigations

* **Complexity perception:** Mitigate via clear UI tags and defaults; keep coefficients for non‑physical types.
* **Balance churn:** Start with limited content; add telemetry; iterate.
* **Double mitigation bugs:** Skip coefficients when `PenHandledFlags` present and honor `physicalCoefficientsDisabled: true` on items with `ArmorRating`.
* **Edge cases (AOE/explosives):** keep existing coefficient model for AOE initially; later consider fragmentation with low‑AP shards.
* **Sequence gaming:** low‑discrepancy schedule + qualifying‑hit gates; scope by `(shooter,target,weaponFamily)`.

---

## 17) Alternatives Considered

1. **Status quo (coefficients only):** simple but arbitrary; poor intuition; injection/bite edge‑cases remain.
2. **Hardcoded ammo rules (HP/FM﻿J/AP) without AP math:** still arbitrary; doesn’t generalize.
3. **Full realism (materials/angles/fragmentation):** high cost; low benefit for gameplay.

---

## 18) Open Questions

* Material presets: finalize baseline AR ranges per item class (soft, ceramic, steel).
* Derive suggested AP from caliber/weapon family when authors omit it?
* Should prone/downed targets adjust head fraction?
* How to expose shield arc to content (per item or per state)?

---

## 19) Appendix: Pseudocode Snippets

### AP↔AR resolve (entry → core → exit, AP‑only)

```text
ResolveHit(hit, proj, target):
  dmg := proj.damage
  apRem := proj.apSharpMm   // or apBluntMm for blunt contests
  typ := PrimaryPhysical(dmg)

  // REGION & FACING
  region := RegionResolver.Resolve(shooter, target, hit) // Head or Body
  facingIsFront := Dot(TargetFacing, -hit.dir) > 0
  shieldEligible := ShieldRaised && Angle(ShieldForward, -hit.dir) <= shieldArc/2

  // ENTRY LAYERS
  for layer in LayersEntry(target, region, facingIsFront, shieldEligible):
    ar := layer.AR(typ)
    if apRem >= ar:
      apRem -= ar
    else:
      // deflect: convert a fraction of sharp to blunt and stop at this layer if plate
      bluntGain := dmg[typ] * proj.deflectBluntScalar * (1 - apRem/ar)^gamma
      dmg[typ] = 0
      dmg[Blunt] += bluntGain
      if layer.stopsOnDeflect: goto post

  // CORE LAYER
  coreAr := (region == Head ? coreHeadArMm : coreBodyArMm)
  if apRem >= coreAr:
    apRem -= coreAr
  else:
    Lodge(); goto post

  // EXIT LAYERS (reverse order, rear coverage only)
  for layer in LayersExit(target, region, !facingIsFront):
    ar := layer.AR(typ)
    if apRem >= ar:
      apRem -= ar
    else:
      Lodge(); break

post:
  // Optional stamina coupling from blunt created on deflect
  if staminaEnabled:
    ApplyStaminaCapped(target, BluntCreated * staminaScalar * DissipationFactor(target))

  MarkPenHandled(dmg)
  return dmg
```

---

**End of draft**
