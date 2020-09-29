# Power BI Source Control

## Presentation
**Power BI Source Control** offers the possibility to extract the contents of Power BI reports (DAX measures, M queries and visuals) into text files that can be compared between different versions. Combined with an integration with the Git version management system, it can be used to automate a change tracking workflow. 

This solution also allows to modify reports without having to open the Power BI Desktop client, saving a lot of time when making mass changes to a series of reports.

### Common use cases
| **Standard workflow** | **Enhanced workflow (with code edition)** |
|:--------------------: |:----------------------------------------: |
| ![Standard workflow](https://github.com/synchrotech/powerbi-source-control/blob/master/Images/standard-workflow.png) | ![Enhanced workflow](https://github.com/synchrotech/powerbi-source-control/blob/master/Images/enhanced-workflow.png) |


## Git installation
It consists of two elements:
 - `PBITUtility.exe` – A utility to extract/import content from/to a PBIT file
 - **Git hooks** – Scripts executed at specific stages of the Git version management process

***Note:** we could imagine integrating this solution into other versioning systems such as TFS, but in this case Git hooks must be replaced with equivalent functionalities or documented manual steps.*

### Setup your repository
This operation must be done only once by repository. 

 1. Create a **Git Hooks** folder at the root of your repository and copy these files into it: 
```
PBITUtility.exe 
post-commit 
post-merge 
pre-commit
```

 2. Add the following lines to the **.gitignore** file: 
```
## Power BI source control.
.commit
*.pbix
```

 3. Commit your changes.

### Configure your client
To enable the automated versioning process on a cloned repository where the solution has been installed, run the following command from the Git CLI: `git config core.hooksPath "Git Hooks"`.
This activates the hooks present in the **Git Hooks** folder.

## Usage

### Way of working
The developer works with a PBIX file. When his changes are completed, he saves them in a PBIT template in the same folder and under the same file name as the report.

The PBIX file is ignored by Git and will never be committed. This is ensured by the configuration made in the **.gitignore** file. By working this way, we make sure that only the structure of the report (and no inappropriate data) is saved to the Git server.

### On commit
On commit, the contents of the PBIT file are automatically extracted into a subfolder suffixed with **.contents** and amended to the commit.

This allows to compare the different versions of the report over time (data model as well as visuals) and to edit the report from a text editor. If you wish to rebuild the template from edited text files, see **Rebuild the templates**.

### On merge
When merging remote changes, if a PBIT file is updated, the corresponding local PBIX file (now obsolete) is automatically deleted to ensure that the developer is always working with the latest version of the report.

## Rebuild the templates
The executable used by the automatic processes (hooks) to export the contents of a PBIT file can also be called directly from a command line prompt. This way, it can be used to rebuild the PBIT file from the manually edited contents, without having to open the Power BI Desktop client. 

It is located in the **Git Hooks** folder.
```
Usage:
	"pbitutility -e <file.pbit>" exports the contents of the PBIT file to flat files.
	"pbitutility -g <folder.pbit.contents>" re-generates a PBIT file from the flat files.
```

You can also drag and drop an item directly onto the exe. 
If it’s a file, it will try to export its contents. If it’s a directory, it will try to re-generate the PBIT file. 

***Warning:** use it at your own risk! Re-generating a PBIT file from flat files can lead to corruption if unexpected changes were made. We suggest using this feature only to perform small changes to M queries and DAX measures.*
