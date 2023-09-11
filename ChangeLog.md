*See prior history in the [Code Changes History](https://github.com/rivantsov/vita/wiki/Code-changes-history) page in Wiki tab of this repo.*

## Version 3.6. Sept 11, 2023. Minor update
* MS SQL driver, batch execution. Added to batch text at start: SET XACT_ABORT ON -- to stop execution on any error and abort trans.
* Deprecated session option EnableSmartLoad, now enabled by default, new opton DisableSmartLoad to disable it explicitly
* SmartLoad implementation: The select-from-multiple style - 'Select ... (where id in ())' - is used only if there's more than 1 sibling parent entity. Seems to be faster when there's only one ID in the list. 
* Upgraded DB provider packages to latest.  
