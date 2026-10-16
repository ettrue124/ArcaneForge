# Battlefield arrival and free-cast menu evidence

Installed client: 0.1.14517.1329972. Inspection used ILSpy against local Core.dll and SharedClientCore.dll.

- StackAgeTracker.Compare sorts title groups and stacks by descending instance-ID age.
- LayoutDataGenerator.GenerateDataForSubRegion starts each row and advances stacks along Vector3.right.
- DefaultEventTranslation.InstanceIdChangeTranslate consumes an InstanceIdChange immediately followed by its ZoneChangeEvent and creates one ZoneTransferUXEvent with old and new identities.
- ObjectIDChangedAnnotationParser reads orig_id/new_id integer details.
- ResolutionCastTranslation.IsResolutionCast requires a resolving card and cast/play actions whose SourceId equals that resolving card. ResolutionCastWorkflow presents the card browser and submits Pass for decline.

Implementation: increasing IDs on genuine battlefield entry, native identity-change/zone-transfer pairs, typed optional free-cast prompts, validated action responses. Phasing preserves identity. Shared prompt handling applies to Play effects with WithoutManaCost; transformed casts retain existing handling.

Correction after user observation: the earlier decreasing-ID change was confirmed deployed in the live trace (lands899999 through899965), but the user still saw new lands on the wrong side and explicitly requested each new land on the far right. World-coordinate progression alone did not establish screen direction. Arrival allocation now increases from500000. This correction still needs visual acceptance after deployment; numeric-order checks do not establish on-screen placement.

Checks: ArrivalReady build and full installed-client self-tests; native identity merge, bounce/re-entry, no repeated events, native resolution-browser routing; actual Ur-Sphinx accept/decline; broad Forge combat151 events; manual mana/undo/Elf; both shock-land payment branches; all three Explore destinations. Result: passed. Actual placement and browser appearance still require visual confirmation after deployment.
