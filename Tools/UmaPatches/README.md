# Local UMA triangle reader fence 01

Local integration patch only; UMA remains an optional separately acquired vendor dependency. Do not publish vendor source or enable/commit GAMESIM_UMA.

The sibling patch is apply_patch data with THREE relative existing vendor targets. Apply only after the physical preimages match the pinned B6838DCA / 4AE265D6 / 13DBCA6A full hashes recorded in the reviewed proposal. This file does not authorize application, Unity import, or a vendor upgrade. Never overwrite a different vendor version or unrelated local changes.

The patch attaches only actual scheduled triangle-reader JobHandles to their exact SubMeshTriangles cache owner, while retaining the original PendingCombine aggregate. Disposal completes that owner dependency before freeing its buffer; SetTriangles retires the old generation first. Both MeshAPI scheduling paths are covered. No generator-wide queue clear, polling, sleeps, IsCompleted-only shortcut, exception suppression, or global completion fence is introduced.

NonSerialized private JobHandle changes the managed sequential layout; inspected source has no binary-layout consumer, but unknown external binary compatibility is unproved. Public nativeTriangles can be borrowed by external code: this patch covers the inspected source schedules, not unknown borrowers. It does not establish the cause or repair of the separate PlayableGraph diagnostic or Persistent-allocation report.

Required validation: useful old-vendor red on BOTH real preparation paths, then the reviewed finite branch/LOD/shared-reader matrix green under the same optional UMA assembly. Keep complete native logs through shutdown; NUnit passes alone do not qualify lifecycle cleanliness. Any missing ownership/setup precondition is an unqualified test, not the intended red. Global-disposal cases must refuse foreign cache membership. Preserve historical failed logs and source floors.
