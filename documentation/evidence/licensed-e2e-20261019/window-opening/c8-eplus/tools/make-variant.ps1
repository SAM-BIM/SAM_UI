param([string]$Src,[string]$Out,[bool]$Latch,[double]$Area,[string]$Cw,[double]$Angle,[double]$DH,[double]$Cd,[double]$CapW=0)
$z='SAM_ThermalZone_GF_08_Bathroom_13a0403a'
$prog=@(
 '  SET GF08_HourDbg = Hour,',
 '  SET GF08_MinDbg = Minute,',
 '  SET GF08_Ramp = (GF08_Tz_Sensor - 22.0) / 2.0,',
 '  SET GF08_Ramp = @Max GF08_Ramp 0.0,',
 '  SET GF08_Ramp = @Min GF08_Ramp 1.0,')
if($Latch){ $prog+=@(
 '  IF Hour == 0,',
 '    SET GF08_LatchEval = 0,',
 '  ENDIF,',
 '  IF (Hour == 23) && (GF08_LatchEval == 0),',
 '    SET GF08_LatchEval = 1,',
 '    IF GF08_Tz_Sensor > 23.0,',
 '      SET GF08_Latch = 1,',
 '    ELSE,',
 '      SET GF08_Latch = 0,',
 '    ENDIF,',
 '  ENDIF,',
 '  IF Hour == 8,',
 '    SET GF08_Latch = 0,',
 '  ENDIF,',
 '  IF ((Hour >= 23) || (Hour < 8)) && (GF08_Latch == 1),',
 '    SET GF08_Frac = 1.0,',
 '  ELSE,',
 '    SET GF08_Frac = GF08_Ramp,',
 '  ENDIF,') } else { $prog+=@('  SET GF08_Frac = GF08_Ramp,') }
$prog+=@('  SET GF08_OpenFrac_Act = GF08_Frac;')
$progText=($prog -join "`r`n")
$blk=@"

!- ===== C8 proof of concept: GF_08 natural ventilation (isolated copy; not product output) =====
ScheduleTypeLimits,
  GF08_Fraction, 0, 1, Continuous;

Schedule:Constant,
  GF08_OpenFrac,                          !- Name
  GF08_Fraction,                          !- Schedule Type Limits Name
  0;                                      !- Hourly Value

ZoneVentilation:WindandStackOpenArea,
  GF08_PartO_Window,                      !- Name
  $z, !- Zone or Space Name
  $Area,                                  !- Opening Area {m2}
  GF08_OpenFrac,                          !- Opening Area Fraction Schedule Name
  $Cw,                                    !- Opening Effectiveness {dimensionless}
  $Angle,                                 !- Effective Angle {deg}
  $DH,                                    !- Height Difference {m}
  $Cd;                                    !- Discharge Coefficient for Opening

EnergyManagementSystem:Sensor,
  GF08_Tz_Sensor,                         !- Name
  $z, !- Output:Variable or Output:Meter Index Key Name
  Zone Mean Air Temperature;              !- Output:Variable or Output:Meter Name

EnergyManagementSystem:Actuator,
  GF08_OpenFrac_Act,                      !- Name
  GF08_OpenFrac,                          !- Actuated Component Unique Name
  Schedule:Constant,                      !- Actuated Component Type
  Schedule Value;                         !- Actuated Component Control Type

EnergyManagementSystem:GlobalVariable,
  GF08_Latch,
  GF08_LatchEval;

EnergyManagementSystem:Program,
  GF08_PartO_Control,                     !- Name
$progText

EnergyManagementSystem:ProgramCallingManager,
  GF08_PartO_Manager,                     !- Name
  BeginTimestepBeforePredictor,           !- EnergyPlus Model Calling Point
  GF08_PartO_Control;                     !- Program Name 1

EnergyManagementSystem:OutputVariable,
  GF08 Opening Fraction Command, GF08_Frac, Averaged, ZoneTimestep, GF08_PartO_Control, ;
EnergyManagementSystem:OutputVariable,
  GF08 Latch State, GF08_Latch, Averaged, ZoneTimestep, , ;
EnergyManagementSystem:OutputVariable,
  GF08 Sensed Tz, GF08_Tz_Sensor, Averaged, ZoneTimestep, , C;
EnergyManagementSystem:OutputVariable,
  GF08 Hour Seen, GF08_HourDbg, Averaged, ZoneTimestep, GF08_PartO_Control, ;
EnergyManagementSystem:OutputVariable,
  GF08 Minute Seen, GF08_MinDbg, Averaged, ZoneTimestep, GF08_PartO_Control, ;

Output:EnergyManagementSystem,
  Verbose, Verbose, ErrorsOnly;

Output:Variable, EMS, GF08 Opening Fraction Command, Hourly;
Output:Variable, EMS, GF08 Latch State, Hourly;
Output:Variable, EMS, GF08 Opening Fraction Command, Timestep;
Output:Variable, EMS, GF08 Latch State, Timestep;
Output:Variable, EMS, GF08 Sensed Tz, Timestep;
Output:Variable, EMS, GF08 Hour Seen, Timestep;
Output:Variable, EMS, GF08 Minute Seen, Timestep;
Output:Variable, $z, Zone Mean Air Temperature, Timestep;
Output:Variable, $z, Zone Ventilation Sensible Heat Loss Energy, Hourly;
Output:Variable, $z, Zone Ventilation Sensible Heat Gain Energy, Hourly;
Output:Variable, $z, Zone Ventilation Current Density Volume Flow Rate, Hourly;
Output:Variable, $z, Zone Ventilation Current Density Air Change Rate, Hourly;
Output:Variable, $z, Zone Infiltration Current Density Volume Flow Rate, Hourly;
Output:Variable, *, Site Wind Speed, Hourly;
Output:Variable, *, Site Outdoor Air Drybulb Temperature, Hourly;
"@
$txt=[IO.File]::ReadAllText($Src); if($CapW -gt 0){ $L=[System.Collections.Generic.List[string]]($txt -split "`r?`n"); $i=-1; for($k=0;$k -lt $L.Count;$k++){ if($L[$k] -match "^\s*SAM_IdealLoads_GF_08_Bathroom_13a0403a,\s+!- Name"){ $i=$k; break } }; if($i -lt 0){ throw "ideal loads not found" }; if($L[$i+9] -notmatch "Heating Limit" -or $L[$i+11] -notmatch "Maximum Sensible Heating Capacity"){ throw "layout" }; $L[$i+9]="  LimitCapacity,                          !- Heating Limit"; $L[$i+11]="  $CapW,                                  !- Maximum Sensible Heating Capacity {W}"; $txt=($L -join "`r`n") }; [IO.File]::WriteAllText($Out,$txt+"`r`n"+$blk)


