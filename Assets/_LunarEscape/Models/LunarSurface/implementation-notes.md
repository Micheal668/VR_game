# Detailed lunar surface implementation

Scope: the main docking scene (10) only. `BuildRealisticLunarSurface.Install()` opens 10; `ApplyToCurrentScene()` accepts a scene with DockingMission and LunarTerrainLayout so the existing 09 → 10 build can apply it before SaveAs. Earlier scene meshes/materials and `LunarTerrainProfile.Height` are unchanged.

## Geometry and height

- `LunarSurfaceProfile.Height` wraps the original terrain with irregular large-crater bowls, narrower broken rims, low ejecta aprons, 20 small depressions and modest intermediate relief. Sub-centimetre grains remain in textures.
- The original base footprint centred at (-2.5, 0), half-size (13.5, 15), landing pad centred at (48, 1.7), radius 7 m, and all three evacuation-route segments retain the original zero-height safety mask. New stones additionally keep 1.4 m plus their radius beyond the base, 8 m plus their radius from the pad centre, and 3.6 m plus their radius from the route centreline.
- Near mesh: unchanged footprint x [-71,121], z [-80,80], 1 m grid, 31,073 vertices / 61,440 triangles. Collider shares this exact asset. Arbitrary off-grid raycasts follow triangle interpolation; they are not expected to equal the continuous profile analytically.
- Far mesh: 16 m interior grid, with original 8 m midpoints inserted around both its near opening and its 1.6 km outer boundary. Only edge cells use centre fans. 10,986 vertices / 21,084 triangles. The 800 outer and 88 inner boundary edges are each exactly 8 m long; winding and manifold-edge counts were checked offline.
- Near/far total: 82,524 triangles, down 41.26% from the former 140,480. Shared colour data uses Color32 and both meshes use 16-bit indices. The global 384,000-triangle mesh is reused unchanged.
- New detail fades completely before the near boundary. Its edge vertices retain the original interpolated 8 m heights. Far/global edge positions sample the original profile; no skirts or overlapping surfaces hide cracks.

## Stones and LOD

Six deterministic irregular breccia forms have shared 80-triangle and 20-triangle meshes, 1,800 unique vertices / 600 unique triangles across all 12 mesh assets. Every stone uses these assets and one instancing-enabled URP material. No static batching duplicates the meshes.

Each view has 224 placed stones: 180 at 8–28 cm, 36 at 32–68 cm and eight at 85–135 cm, with varied axes and orientation. Most are clustered irregularly around crater rims; the rest are sparse, and all avoid steep/occupied/safety areas. Placement samples the actual baked near triangles and partially buries the stones. Each LODGroup drops from 80 to 20 triangles, then culls; no cross-fade overdraw. Only medium and large stones cast shadows.

The ground root is `Realistic Lunar Rocks`; the flight root is `Flight Realistic Lunar Rocks`. Both are siblings of the existing terrain root, so the core terrain retains exactly three renderers. Both copies share every mesh and material. Ground and flight are mutually exclusive through the existing presenter. Eight ground large stones have simple sphere colliders. Small/medium stones and the entire flight copy have none.

Worst-case near + far + all stones at LOD0 is 100,444 triangles per active view, still 28.5% below the previous terrain alone. Real viewing distances select LOD1 or cull small stones earlier.

## Material and texture memory

The new `Detailed Continuous Moon` shader preserves the original global spherical coordinate mapping, moon centre/radius, SurfaceAngle and RenderScale behaviour. All three terrain surfaces use one shared new material referencing the original LRO global albedo. Global geometry, celestial transforms, the sky and Earth are not changed. The original shader and material remain available for earlier lessons.

Two repeatable 1024×1024 textures are baked once by editor code: sRGB RGB albedo with linear roughness in alpha, and a tangent-space normal map. Sand, embedded angular grains and tiny pits are baked into that set. The same textures are reused by the stones. Source generation is deterministic and original procedural work, with no new external asset/license dependency.

Both textures use trilinear mipmaps, 4× anisotropy and Read/Write disabled. Standalone compression is BC7 + BC5: conservatively 2.667 MiB including mip chains. Android overrides use ASTC 6×6. No new 2K/4K/8K texture is introduced. Source PNG size and transient editor-generation arrays are not runtime GPU memory.

Near material detail fades by true distance and pixel footprint; far material still uses the original broad lunar appearance. Expensive procedural distant-crater shading only runs when its distant blend is non-zero. Rocks use the existing URP Lit shader for standard GPU instancing and shadows. No component generates or rewrites meshes/textures at runtime.

`resource-manifest.json` is emitted on Apply from the actual baked mesh assets. It reports both unique buffers and a conservative CPU+GPU duplication estimate, texture formats, instance counts and collider counts. Mesh CPU data is intentionally retained for collider cooking and editor/test validation; the manifest includes that cost. Global mesh and the original LRO texture are reused resources and are called out separately, rather than counted as new additions.

## Verification performed before integration

- Offline C# compilation of the builder against the project's Unity 6000.6.1f1 assemblies: passed, no compiler warnings/errors.
- Independent far-topology check: 10,986 vertices, 21,084 correctly wound triangles, no non-manifold edges, exactly 888 open perimeter edges (800 outer + 88 inner), all original 8 m segments.
- The parent integration run is responsible for Unity shader/import checks, generated manifest, scene screenshots, route/flight/LOD tests and any visual adjustment. This note does not claim those integration checks were run by the surface builder author.

## Material persistence validation

The surface material explicitly copies only the original global base texture/tint and shared scalar settings. It does not replace the new shader's serialized property table with `CopyPropertiesFromMaterial`. After assigning `_DetailAlbedo`, `_DetailNormal`, `_DetailScale` and `_NormalStrength`, the builder validates their values, saves the material immediately, forces a synchronous asset reimport, reloads and validates it again. A second reload check follows all mesh/manifest imports. Install also writes the post-install ArtResourceAudit report. These checks fail the bake if a later import loses any detail binding.
