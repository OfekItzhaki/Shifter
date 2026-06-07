# Shifter Yokneam Acceptance Flow

This flow turns the Hebrew field-test idea into repeatable checks for Shifter.

## Manual Product Flow

1. Register a new admin account.
2. Create a space/group named `פלוגת יוקנעם`.
3. Add soldiers with realistic names.
4. Add operational roles:
   - `מ"מ`
   - `סמל`
   - `מ"כ`
   - `חייל`
5. Add qualifications:
   - `מפקד סיור`
   - `מפקד כיתת כוננות`
   - `נהג סיור`
   - `מפעיל רחפן`
   - `סמב"צ חמל`
   - `מפקד חמל`
   - `מפקד יזומה`
6. Add tasks/missions:
   - `סיור יוקנעם`
   - `חמ"ל`
   - `כיתת כוננות`
   - `רחפן`
   - `יזומה`
7. Add absences and constraints:
   - Abroad / `חו"ל`
   - Sick days / `ימי מחלה`
   - Personal or company events
8. Run Shifter to create the first draft.
9. Review the draft:
   - People absent from base are not assigned.
   - Patrols include a patrol driver and patrol commander.
   - HQ shifts include a HQ operator and HQ commander.
   - Drone shifts use a drone operator.
   - Minimum rest is respected before publishing.
10. Publish the plan.
11. Add a new developing constraint, such as a soldier becoming unavailable.
12. Run Shifter again and verify a new draft is created from the new reality.
13. Enter as a soldier:
   - View personal missions.
   - Download/export the personal plan.
14. Enter as a commander:
   - View and export the platoon/company plan according to permissions.
   - Receive updates when a new plan is published.

## Automated Checks

The deterministic backend acceptance test is:

```powershell
dotnet test apps\api\Jobuler.Tests\Jobuler.Tests.csproj --filter "FullyQualifiedName~YokneamCompanySchedulingFlowTests"
```

It seeds `פלוגת יוקנעם` with soldiers, roles, qualifications, absences, and missions, then verifies the Shifter planning payload contains the right eligible people, role IDs, qualification requirements, and evolving absences.

The live optimizer contract tests are:

```powershell
dotnet test apps\api\Jobuler.Tests\Jobuler.Tests.csproj --filter "FullyQualifiedName~SolverEndToEndTests"
```

Those require the Python Shifter service running on `http://localhost:8000`.
