### ProcessorFramework
The successor to Universal Fermenter
 
Processor Framework is a framework for autonomous transformation of ingredients into products inside objects. The framework is largely based on the original universal fermenter, with some large-scale improvements.

Main Features:
* Independent processes
* Parallel processes
* ProcessDefs
* New UI

`Indepedent processes` allows stacks of items to process independently. This enables products to be retrieved as soon as their stack is complete, indepedent of other stacks. If turned off stacks will be combined as in vanilla beer fermenting.

`Parallel processes` allows different kinds of processes to run concurrently. This can be combined with indepedent processes.

`ProcessDefs` carry most settings for how items are processed and will be referenced by their `defName` in the `ProcessorComp`. This allows parent ProcessDefs to be created and used, reducing work for modders.

`New UI` features a custom Inspector Tab that lists available products with ingredients for those products as sub-nodes, allowing players to easily understand what can be used to make a product and allowing or disallowing specific products or ingredients.

![](https://i.imgur.com/PFrs6xH.gif)
