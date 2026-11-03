# Tunnel portal repair

Both surface portals now have closed retaining-wall ends and a solid headwall
following the tunnel arch. Outer roof edges join the retaining-wall shell.
The open ramp parapets stay at world Y = 0.7 m, rather than rising to 4.24 m at
the surface entrances. Road width, centerline and ramp gradients are unchanged.

A concrete apron mesh, with matching collision, covers the actual terrain-hole
cell boundaries at each mouth, both sides, and the transition over the roof.
The apron overlaps the terrain by 0.15 m and leaves the 14 m roadway opening free.
This conceals the raster cut without enlarging the holes or changing TerrainData.

The saved facility prefab references the apron and the repaired structural mesh.
The structural asset GUID is preserved. The Sandbox scene, vehicle, lighting,
controls, camera zones and original terrain remain unchanged.

To regenerate in place, close the project in Unity and run:

```
python3 tools/sandbox-expansion/run.py ExpansionAuthoring.RepairPortals
```

Validation on Unity 6000.6.7f1:

```
python3 tools/sandbox-expansion/run.py PortalVerify.Run
python3 tools/sandbox-expansion/run.py FacilityVerify.Run
python3 tools/sandbox-expansion/run.py FacilityPreview.Run --graphics
```

PortalVerify checks 1,499 native physics queries/conditions for removed terrain
cells outside the roadway, capped wall ends, arch headwalls, continuous support
and unobstructed entry lanes at both mouths. FacilityVerify covers the existing
road, walls, roof, six-wheel contacts, physical traversal and camera restoration.
Native HDRP captures include side views and arch approaches at both portals.
Reports are in TestResults/SandboxExpansion. Checks were run in an isolated copy
because the original project was open; only the repaired assets were transferred.
