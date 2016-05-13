## Changelog
### Features
- added DataSet schema definition endpoint
- hierarchical interpreted fields

---

Slamby introduces Slamby Service (API). Build powerful data management center, store and analyze your data easily. This documentation shows you a quick overview about the available API endpoints, technical details and business usage.
With Slamby you can:
* Store and manage your data easily
* Get high data security and privacy
* Use improved data analysis

Once you've
[registered your client](http://slamby.com/register/) it's easy
to start working with Slamby API.

All endpoints are only accessible via https and are located at
`api.slamby.com`.
```
    https://api.slamby.com/CLIENT_ID
```

> **Tip:** The `CLIENT_ID` is your unique identifier what you get after your server is ready to use.


### Authentication
The base of the authentication is the `API_KEY`.
You have to provide it in the authorization header. It is **required for every API call**.
The examples of the documentation is preasuming that you provide the API key in the header.

>*Example*
>
Header   |Value
---------|---
Authorization|Slamby `API_KEY`

&nbsp;

>**Tip:** You will get your `API_KEY` after your server is ready to use.

### API Version

Every response contains API version information in response HTTP `X-Api-Version` header.

>*Example*
>
X-Api-Version: 0.11.0

## Dataset
Slamby provides **Dataset** as a data storage. A dataset is a JSON document storage that allows to store schema free JSON objects, indexes and additional parameters. Inside your server you can create and manage multiple datasets.

With dataset you can:
* Create multiple datasets
* Using schema free JSON objects
* Set indexes for text processing
* Running text analysis on the stored data

> **Tip:** With schema free JSON storage you can easily store your existing schemas. Store document related data - such as image urls, prices - and build powerful queries.

### Create new Dataset
Create a new dataset by providing a sample JSON document and additional parameters.

*Example REQUEST*

> [POST /api/DataSets](#operation--api-DataSets-post))
```JSON
{
    "IdField": "id",
    "InterpretedFields": ["title", "desc"],
    "Name": "test1",
    "NGramCount": "3",
    "TagField": "tag",
    "SampleDocument": {
        "id": 9,
        "title": "Example Product Title",
        "desc": "Example Product Description",
        "tag": [1,2,3]
    }
}
```

Create a new dataset by providing a schema JSON document and additional parameters.

*Example REQUEST*

> [POST /api/DataSets/Schema](#operation--api-DataSets-Schema-post))
```JSON
{
    "IdField": "id",
    "InterpretedFields": [
        "title",
        "desc"
    ],
    "Name": "test2",
    "NGramCount": "3",
    "TagField": "tag",
    "Schema": {
        "type": "object",
        "properties": {
            "id": {
                "type": "integer"
            },
            "title": {
                "type": "string"
            },
            "desc": {
                "type": "string"
            },
            "tag": {
                "type": "array",
                "items": {
                    "type": "byte"
                }
            }
        }
    }
}
```

*Example RESPONSE*
>HTTP/1.1 201 CREATED

##### Check the DataSet schema definition [here](#/definitions/DataSet)



### Data Types

Defining a dataset schema you can set your custom field type.

*Currently available field types:*

Name    |   Types
--- |   ---
String  |   `string`
Numeric |   `long`, `integer`, `short`, `byte`, `double`, `float`
Date    |   `date`
Boolean |   `boolean`
Array   |   `array`
Object  |   `object` for single JSON objects

*Example schema*

```JSON
{
    "type": "object",
    "properties": {
        "name": {
            "type": "object",
            "properties": {
                "firstName": {
                    "type": "string"
                },
                "secondName": {
                    "type": "string"
                }
            },
        "age": {
            "type":"integer"
        },
        "sex": {
            "type":"boolean"
        },
        "luckyNumbers": {
            "type": "array",
            "items": {
                type: "integer"
            }
        }
    }
}
```

### Date Formats

You can define your custom date format to specify your needs.
For dataset date formats you can use the built-in [elastic-search custom formats](https://www.elastic.co/guide/en/elasticsearch/reference/2.2/mapping-date-format.html).
If you do not provide date format, default value is `"strict_date_optional_time||epoch_millis"`.

**Built in formats e.g.**

name    |   Description
--- |   ---
`epoch_millis`    |   A formatter for the number of milliseconds since the epoch. Note, that this timestamp allows a max length of 13 chars, so only dates between 1653 and 2286 are supported. You should use a different date formatter in that case. 
`epoch_second`    |   A formatter for the number of seconds since the epoch. Note, that this timestamp allows a max length of 10 chars, so only dates between 1653 and 2286 are supported. You should use a different date formatter in that case. 
`date_optional_time` or `strict_date_optional_time` |    A generic ISO datetime parser where the date is mandatory and the time is optional.
`basic_date`  |   A basic formatter for a full date as four digit year, two digit month of year, and two digit day of month: yyyyMMdd.
`basic_date_time` |   A basic formatter that combines a basic date and time, separated by a T: yyyyMMdd'T'HHmmss.SSSZ.
`basic_date_time_no_millis`   |   A basic formatter that combines a basic date and time without millis, separated by a T: yyyyMMdd'T'HHmmssZ. 
`basic_ordinal_date`  |   A formatter for a full ordinal date, using a four digit year and three digit dayOfYear: yyyyDDD. 
...

### Get Dataset
Get information about a given dataset. A dataset can be accessed by its name.

Returns with:
* Dataset basic information
* Dataset settings
* Schema sample document
* Dataset statistics

*Example REQUEST*
> [GET /api/DataSets/`example`](#operation--api-DataSets-get)

*Example RESPONSE*
> HTTP/1.1 200 OK
```JSON
{
    "Name": "example",
    "NGramCount": 3,
    "IdField": "id",
    "TagField": "tag",
    "InterpretedFields": [
    "title",
    "desc"
    ],
    "Statistics": {
    "DocumentsCount": 3
    },
    "SampleDocument": {
    "id": 1,
    "title": "Example title",
    "desc": "Example Description"
    "tag": [1,2,3]
    }
}
```

##### Check the DataSet schema definition [here](#/definitions/DataSet)

### Get Dataset List
Get a list of the available datasets.

Returns with:
* Dataset objects array

*Example REQUEST*
> [GET /api/DataSets](#operation--api-DataSets-get)

*Example RESPONSE*
> HTTP/1.1 200 OK
```JSON
[
    {
    "Name": "example",
    "NGramCount": 3,
    "IdField": "id",
    "TagField": "tags",
    "InterpretedFields": [
        "title",
        "desc"
    ],
    "Statistics": {
        "DocumentsCount": 3
    },
    "SampleDocument": {
        "id": 1,
        "title": "Example title",
        "desc": "Example Description"
        "tags": [1,2,3]
    }
    },
    {
    "Name": "example2",
    "NGramCount": 3,
    "IdField": "id",
    "TagField": "tags",
    "InterpretedFields": [
        "title",
        "desc"
    ],
    "Statistics": {
        "DocumentsCount": 3
    },
    "SampleDocument": {
        "id": 1,
        "title": "Example title",
        "desc": "Example Description"
        "tags": [1,2,3]
    }
    }
]
```

##### Check the DataSet schema definition [here](#/definitions/DataSet)

### Remove Dataset
Removes a given dataset. All the stored data will be removed.

*Example REQUEST*
> [DELETE /api/DataSets/`example`](#operation--api-DataSets-delete)

*Example RESPONSE*
> HTTP/1.1 200 OK

## Document
Manage your **documents** easily. Create, edit, remove and running text analysis.

Every document is related to a dataset. You have to specify which dataset you want to use in the `X-DataSet` header by the name of the dataset.

> **Tip:** If you use any of the Document methods without or an unexisting `X-DataSet` header you will get a `Missing X-DataSet header!` error.

With document you can:
* Insert multiple documents
* Using your own schema
* Accessing your documents easily
* Modifying your documents easily
* Running text analysis

> **Tip:** Store all the related information - such as text, prices, image urls - and use powerful queries.

### Insert New Document
Insert a new document to a dataset using the predefined schema.

*Example REQUEST*
> [POST /api/Documents](#operation--api-Documents-post)
>
Header   |Value
---------|---
X-DataSet|example
>
```JSON
{
    "id": 9,
    "title": "Example Product Title",
    "desc": "Example Product Description",
    "tags": [1,2,3]
}
```

*Example RESPONSE*
> HTTP/1.1 201 CREATED

### Get Document
Get a document from a dataset.

*Example REQUEST*
> [GET /api/Documents/`9`](#operation--api-Documents-get)
>
Header   |Value
---------|---
X-DataSet|example

*Example RESPONSE*
> HTTP/1.1 200 OK
```JSON
{
    "id": 9,
    "title": "Example Product Title",
    "desc": "Example Product Description",
    "tags": [1,2,3]
}
```

### Edit Document
Edit an existing document in a dataset.

*Example REQUEST*
> [PUT /api/Documents/`9`](#operation--api-Documents-put)
>
Header   |Value
---------|---
X-DataSet|example
```JSON
{
    "id": 9,
    "title": "Example Modified Product Title",
    "desc": "Example Modified Product Description",
    "tags": [1,2,3,4,5,6,7,8,9]
}
```

*Example RESPONSE*
> HTTP/1.1 200 OK

### Delete Document
Delete an existing document in a dataset.

*Examle REQUEST*
> [DELETE /api/Documents/`9`](#operation--api-Documents-delete)
>
Header   |Value
---------|---
X-DataSet|example


*Example RESPONSE*
> HTTP/1.1 200 OK

### Copy To
Copying documents from a dataset to another one. You can specify the documents by id. You can copy documents to an existing dataset.
The selected documents will **remain in the source dataset** as well.

*Example REQUEST*
> [POST /api/Documents/Copy](#operation--api-Documents-Copy-post)
>
Header   |Value
---------|---
X-DataSet|example
```JSON
{
    "DestinationDataSetName": "TARGET_DATASET_NAME",
    "Ids": ["10", "11"]
}
```

*Example RESPONSE*
> HTTP/1.1 200 OK

> **Tip:** You can use the [POST /api/Documents/Sample](#operation--api-Documents-Sample-post) or the [POST /api/Documents/Filter](#operation--api-Documents-Filter-post) methods to get document ids easily.

### Move To

Moving documents from a dataset to another one. You can specify documents by id. You can move documents to an existing dataset. 
The selected documents will be **removed from the source dataset**.

*Example REQUEST*
> [POST /api/Documents/Move](#operation--api-Documents-Move-post)
>
Header   |Value
---------|---
X-DataSet|example
```JSON
{
    "DestinationDataSetName": "TARGET_DATASET_NAME",
    "Ids": ["10", "11"]
}
```

*Example RESPONSE*
> HTTP/1.1 200 OK

> **Tip:** You can use the [POST /api/Documents/Sample](#operation--api-Documents-Sample-post) or the [POST /api/Documents/Filter](#operation--api-Documents-Filter-post) methods to get document ids easily.



## Tags
Manage tags to organize your data. Using tags create a tag cloud or a hiearchical tag tree.

Every tag is related to a Dataset. You have to specify which dataset you want to use in the `X-DataSet` header by the name of the dataset.

> **Tip:** If you use any of the tag methods without or an unexisting `X-DataSet` header you will get a `Missing X-DataSet header!` error.

With Tags you can:
* Create new tag
* Update a tag
* Get a single tag or a full tag list
* Organize your tags into hierarchy
* Use tags for categorization
* Use tags for tagging.

### Create New Tag
Create a new tag in a dataset.

>**Tip:** To create hierarchy you have to specify the ParentId of the tag. The ParentId is the Id of the parent of the tag. In the dataset there must be an existing tag with the id given in the ParentId. If the tag is a root element, or you don't want to use hierarchy then just skip the property or set to `null`.

*Example REQUEST*
> [POST /api/Tags](#operation--api-Tags-post)
>
Header   |Value
---------|---
X-DataSet|example
```JSON
{
    "Id": "1",
    "Name": "example tag 1",
    "ParentId": null
}
```

*Example RESPONSE*
> HTTP/1.1 201 CREATED


### Get Tag
Get a tag by its Id. Provide 'withDetails=true' query parameter in order to get DocumentCount, WordCount values. Default value is 'false' because it takes time to calculate these properties.

*Example REQUEST*
> [GET /api/Tags/`1`?withDetails=false](#operation--api-Tags-get)
>
Header   |Value
---------|---
X-DataSet|example
    
*Example RESPONSE*
> HTTP/1.1 200 OK
```JSON
{
    "Id": "1",
    "Name": "example tag 1",
    "ParentId": "5",
    "Properties": {
    "Path": [
        {
        "Id": "5",
        "Level": "1",
        "Name": "example parent tag 5"
        }
        ],
    "Level": 2,
    "IsLeaf": "false",
    "DocumentCount": 33,
    "WordCount": 123
    }
}
```

### Get Tag List
Get all tags list from a given dataset. Provide 'withDetails=true' query parameter in order to get DocumentCount, WordCount values. Default value is 'false' because it takes time to calculate these properties.

*Example REQUEST*
> [GET /api/Tags?withDetails=false](#operation--api-Tags-get)
>
Header   |Value
---------|---
X-DataSet|example

*Example RESPONSE*
> HTTP/1.1 200 OK
```JSON
[
    {
    "Id": "1",
    "Name": "example tag 1",
    "ParentId": null,
    "Properties": {
        "Path": [],
        "Level": 1,
        "IsLeaf": true,
        "DocumentCount": 0,
        "WordCount": 0
    }
    },
    {
    "Id": "2",
    "Name": "example tag 2",
    "ParentId": null,
    "Properties": {
        "Path": [],
        "Level": 1,
        "IsLeaf": true,
        "DocumentCount": 0,
        "WordCount": 0
    }
    }
]
```

### Update Tag

*Example REQUEST*
> [PUT /api/Tags/`1`](#operation--api-Tags-put)
>
Header   |Value
---------|---
X-DataSet|example
```JSON
{
    "Id": "1",
    "Name": "example tag 1",
    "ParentId": null
}
```

*Example RESPONSE*
> HTTP/1.1 200 CREATED

### Remove Tag
Remove a tag from tag list. Default behavior is that only leaf elements can be deleted. You should provide 'force=true' query parameter in order to remove tags with child elements. 'cleanDocument'. Setting 'cleanDocuments=true' removes the specified tag also from its documents.

*Example REQUEST*
> [DELETE /api/Tags/`1`?force=false&cleanDocuments=false](#operation--api-Tags-delete)
>
Header   |Value
---------|---
X-DataSet|example

*Example RESPONSE*
> HTTP/1.1 200 OK

## Sampling
Statistical method to support sampling activity. Using sampling you can easily create **random samples** for experiments.

With sampling you can:
- Create sample easily
- Set the source categories
- Use normal or stratified sampling method
- Set sample size by fix number or percentage
- Use built in pagination.

For sampling you have to specify which dataset you want to use in the `X-DataSet` header by the name of the dataset.

*Example REQUEST*
> [POST /api/Documents/Sample](#operation--api-Documents-Sample-post)
>
Header   |Value
---------|---
X-DataSet|example
```JSON
{
    "Id" : "6902a2d3-0708-41f7-b21d-c5bd4b302bdc",
    "IsStratified" : "false",
    "Percent" : "0",
    "Size" : "15000",
    "TagIds" : [],
    "Pagination" : {
        "Offset" : 0,
        "Limit": 100,
        "OrderDirection" : "Asc",
        "OrderByField" : "id"
    }
}
```

*Example RESPONSE*
> HTTP/1.1 200 OK
```JSON
{
    "Items": [
    {
        "id": "1455197295447",
        "title": "example title",
        "desc": "example description",
        "tags": [
        "2",
        "3"
        ]
    },
    {
        "id": "1455197591439",
        "title": "example title",
        "desc": "example description",
        "tags": [
        "3"
        ]
    },
    ...
    ],
    "Pagination": {
    "Offset": 0,
    "Limit": 100,
    "OrderDirection": "Asc",
    "OrderByField": "desc"
    },
    "Count": 100,
    "Total": 15000
}
```

##### For the parameters explanation check the DocumentSampleSettings schema definition [here](#/definitions/DocumentSampleSettings)
##### For the pagination explanation check the pagination section [here](#pagination)

## Filter
Powerful **search engine**. Build **smart** search functions or filters. Easily access to your datasets with **simple queries**, **logical expressions** and **wild cards**. Manage your language dependencies using **optinal tokenizer**.

With Filter you can:
* Create simple search queries
* Filter by tags
* Search in multiple fields
* Access to all the available document fields and parameters
* Use logical expressions
* Use wild cards
* Use optional tokenizers
* Use built in pagination

*Example REQUEST*
> [POST /api/Documents/Filter](#operation--api-Documents-Filter-post)
>
Header   |Value
---------|---
X-DataSet|example
```JSON
{
    "Filter" : {
        "TagIds" : ["1"],
        "Query" : "title:michelin"
    },
    "Pagination" : {
        "Offset" : 0,
        "Limit": 100,
        "OrderDirection" : "Asc",
        "OrderByField" : "title"
    }
}
```

*Example RESPONSE*
> HTTP/1.1 200 OK
```JSON
{
    "Items": [
    {
        "id": "1455197455453",
        "title": "example title 1",
        "desc": "example description",
        "tags": [
        "1"
        ]
    },
    {
        "id": "1455197455203",
        "title": "example title 2",
        "desc": "example description",
        "tags": [
        "1"
        ]
    },
    ...
    ],
    "Pagination": {
    "Offset": 0,
    "Limit": 100,
    "OrderDirection": "Asc",
    "OrderByField": "title"
    },
    "Count": 100,
    "Total": 1543
}
```

##### For the parameters explanation check the DocumentFilterSettings schema definition [here](#/definitions/DocumentFilterSettings)
##### For the pagination explanation check the pagination section [here](#pagination)


> **Tip:** Easily create a powerful search engine by using tokenizer and detailed search queries.

## Services
Slamby introduces services. You can quickly create a data processing service from the available service templates. Manage your data processing with services, run different tests, run more data management services parallelly.

**Service definition:** a data management service with custom settings, dedicated resources and available API endpoint.

With services you can:
* Create a service
* Get your services list
* Get a service
* Remove a service
* Manage processes

### Get Service
You can get general information about a service using the Id of the service

*Example REQUEST*
> [GET /api/Services/`GUID`](#operation--api-Services-get)

*Example RESPONSE*
> HTTP/1.1 200 OK
```JSON
{
    "Id": "57c845dc-6aa4-475c-bbf2-0d682f471f32",
    "Name": "Example name of a service",
    "Description": "This is an example service description",
    "Status": "New",
    "Type": "Classifier",
    "ProcessIdList": [
    "e251dbbf-04ff-4d34-a959-90dc4a602142",
    "d335edaf-354a-482c-ade4-4d8172f81a40"
    ],
    "ActualProcessId": null
}
```

### Create New Service
Create a new Service

*Example REQUEST*
> [POST /api/Services](#operation--api-Services-post)
```JSON
{
    "Name": "Example name of a service",
    "Description": "This is an example service description",
    "Type" : "Classifier"
}
```

*Example RESPONSE*
> HTTP/1.1 201 CREATED

### Update Service
You can update only the Name and the Description field.
*Example REQUEST*
> [PUT /api/Services/`GUID`](#operation--api-Services-put)
```JSON
{
    "Name": "Updated example name of a service",
    "Description": "This is an updated example service description"
}
```

*Example RESPONSE*
> HTTP/1.1 200 CREATED


    ### Remove Service
You remove a service anytime. If it's in Activated status then it will be Deactivated first. If it's in Busy status then it will be cancelled first.

*Example REQUEST*
> [DELETE /api/Services/`GUID`](#operation--api-Services-delete)

*Example RESPONSE*
> HTTP/1.1 200 OK


## Classifier Service
Service for text classification. Create a classifier service from a selected dataset, specify your settings and use this service API endpoint to classify your incoming text.

> Currently Slamby provides `Slamby Twister` as a highly accurate classification algorithm designed for e-commerce market.

### Get Classifier Service
You can get classifier specified information about a classifier service with the Id of the service

*Example REQUEST*
> [GET /api/Services/Classifier/`GUID`](#operation--api-Services-Classifier-get)

*Example RESPONSE*
> HTTP/1.1 200 OK
```JSON
{
    "Id": "83326e75-dd16-4e5c-a66d-5ea5197bd8e0",
    "Name": "Example name of a classifier service",
    "Description": "This is an example classifier service description",
    "Status": "New",
    "Type": "Classifier",
    "ProcessIdList": null,
    "ActualProcessId": null,
    "PrepareSettings": null,
    "ActivateSettings": null
}
```

### Prepare Classifier Service
Training Process Steps:
1. Give a suitable name to the service,
2. Set the ngram values,
3. Provide the tag ids the you are going to use during the training,
4. Start the training process.

> For Training process Slamby is using `Slamby Twister` as its own classification algorithm. 

This request is a long running task so the API do it in async way. Therefore the response is a Process.

> `N-gram seetings`: each dataset has an n-gram setting. For set the required n-gram the minimum value is 1, the maximum value equals with the maximum n-gram number of the given dataset. Using a [1,2,3] n-gram settings means during the training process the classifier is going to create 1,2,3 n-gram dictionaries. [Learn more about N-gram](https://en.wikipedia.org/wiki/N-gram)

*Example REQUEST*
> [POST /api/Services/Classifier/`GUID`/Prepare](#operation--api-Services-Classifier-Prepare-post)
```JSON
{
    "DataSetName" : "test dataset",
    "NGramList": [1,2],
    "TagIdList": ["tag1Id","tag2Id"]
}
```

*Example RESPONSE*
> HTTP/1.1 202 ACCEPTED
```JSON
{
    "Id": "d335edaf-354a-482c-ade4-4d8172f81a40",
    "Start": "2016-03-16T13:12:15.5520625Z",
    "End": "0001-01-01T00:00:00",
    "Percent": 0,
    "Status": "InProgress"
}
```

### Activate Classifier Service
Each service has two status: active, deactive. When a preparation/training process is ready, the service has a deactivated status. A deactivated service is ready, but its not loaded into memory and the API is not able to process the incoming requests. To use a service set the status to Activated. After the activation process the service is ready to use, all the required files are loaded and stored in memory, the API endpoint is active.

*Example REQUEST*
> [POST /api/Services/Classifier/`GUID`/Activate](#operation--api-Services-Classifier-Activate-post)
```JSON
{
    "NGramList": [1,2],
    "EmphasizedTagIdList": null,
    "TagIdList": null
}
```

*Example RESPONSE*
> HTTP/1.1 200 OK

### Deactivate Classifier Service
When a service is not needed for continous usage you can deactivate it. After deactivating a service, all the settings and files remain, but they are not using any resources (memory, cores). You can store your deactivated services and activate them anytime.

*Example REQUEST*
> [POST /api/Services/Classifier/`GUID`/Deactivate](#operation--api-Services-Classifier-Deactivate-post)

*Example RESPONSE*
> HTTP/1.1 200 OK

### Recommend
Built-in text classification engine. Uses the prepared Classifier dictionaries and calculations. High speed and classification capability. Built-in n-gram analyzer.

*Example Request*
> [POST /api/Services/Classifier/`GUID`/Recommend](#operation--api-Services-Classifier-Recommend-post)
```JSON
{
    "Text": "Lorem Ipsum Dolorem",
    "Count": "2",
    "UseEmphasizing": false,
    "NeedTagInResults": true,
    
}
```

*Example Response*
```JSON
[
    {
    "TagId": "324",
    "Score": 0.35175663155586434,
    "Tag": {
        "Id": "324",
        "Name": "Tag name",
        "ParentId": "16",
        "Properties": null
    }
    },
    {
    "TagId": "232",
    "Score": 0.30277479057126688,
    "Tag": {
        "Id": "232",
        "Name": "Tag name",
        "ParentId": "24",
        "Properties": null
    }
    }
]
```

### Export dictionaries

*Example Request*
> [POST /api/Services/Classifier/`GUID`/ExportDictionaries](#operation--api-Services-Classifier-ExportDictionaries-post)
```JSON
{
    "NGramList": [1],
    "TagIdList": null
}
```

> **Tip:** If you skip the `TagIdList` or set it to `null` than the API will use all the leaf tags

*Example Response*
```JSON
{
    "Id": "345e1c79-dc78-427f-8ad1-facce75f6ae3",
    "Start": "2016-04-18T13:29:15.3728991Z",
    "End": "2016-04-18T13:29:39.3144202Z",
    "Percent": 0,
    "Description": "Exporting dictionaries from Classifier service prc...",
    "Status": "Finished",
    "Type": "ClassifierExportDictionaries",
    "ErrorMessages": [],
    "ResultMessage": "Successfully exported dictionaries from Classifier service prc!\nExport file can be download from here: https://api.slamby.com/demo-api/files/345e1c79-dc78-427f-8ad1-facce75f6ae3.zip"
}
```

## Prc Service

### Get Prc Service

*Example REQUEST*
> [GET /api/Services/Prc/`GUID`](#operation--api-Services-Prc-get)

*Example RESPONSE*
> HTTP/1.1 200 OK
```JSON
{
    "PrepareSettings": null,
    "ActivateSettings": null,
    "Id": "996f91f4-f1ca-428b-af1a-929ccf3b0243",
    "Name": "prc",
    "Description": null,
    "Status": "New",
    "Type": "Prc",
    "ProcessIdList": [],
    "ActualProcessId": null
}
```

### Prepare Prc Service

*Example REQUEST*
> [POST /api/Services/Prc/`GUID`/Prepare](#operation--api-Services-Prc-Prepare-post)
```JSON
{
    "DataSetName" : "test dataset",
    "TagIdList": ["tag1Id","tag2Id"]
}
```

*Example RESPONSE*
> HTTP/1.1 202 ACCEPTED
```JSON
{
    "Id": "ceed2045-70fb-4785-b483-aadb9bf9a992",
    "Start": "2016-04-18T13:09:49.6032716Z",
    "End": "0001-01-01T00:00:00",
    "Percent": 0,
    "Description": "Preparing Prc service prc...",
    "Status": "InProgress",
    "Type": "PrcPrepare",
    "ErrorMessages": [],
    "ResultMessage": ""
}
```

### Activate Prc Service

*Example REQUEST*
> [POST /api/Services/Prc/`GUID`/Activate](#operation--api-Services-Prc-Activate-post)
```JSON
{
    "FieldsForRecommendation": ["title"]
}
```

*Example RESPONSE*
> HTTP/1.1 200 OK

### Deactivate Prc Service

*Example REQUEST*
> [POST /api/Services/Prc/`GUID`/Deactivate](#operation--api-Services-Prc-Deactivate-post)

*Example RESPONSE*
> HTTP/1.1 200 OK

### Recommend

*Example Request*
> [POST /api/Services/Prc/`GUID`/Recommend](#operation--api-Services-Prc-Recommend-post)
```JSON
{
    "Text": "Lorem Ipsum Dolorem",
    "Count": "2",
    "Filter": null,
    "Weights": null,
    "TagId": "tag1Id",
    "NeedDocumentInResult": true
}
```

*Example Response*
```JSON
[
    {
    "DocumentId": "1777237",
    "Score": 0.89313365295595715,
    "Document": {
        "id": "1777237",
        "tag_id": "tag1Id",
        "title": "Lorem",
        "body": "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor"
    }
    },
    {
    "DocumentId": "6507461",
    "Score": 0.7894283811358983,
    "Document": {
        "ad_id": "6507461",
        "tag_id": "tag1Id",
        "title": "Duis aute irure dolorem",
        "body": "Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur."
    }
    }
]
```

### Export dictionaries

*Example Request*
> [POST /api/Services/Prc/`GUID`/ExportDictionaries](#operation--api-Services-Prc-ExportDictionaries-post)
```JSON
{
    "TagIdList": null
}
```

> **Tip:** If you skip the `TagIdList` or set it to `null` than the API will use all the leaf tags

*Example Response*
```JSON
{
    "Id": "345e1c79-dc78-427f-8ad1-facce75f6ae3",
    "Start": "2016-04-18T13:29:15.3728991Z",
    "End": "2016-04-18T13:29:39.3144202Z",
    "Percent": 0,
    "Description": "Exporting dictionaries from Prc service prc...",
    "Status": "Finished",
    "Type": "PrcExportDictionaries",
    "ErrorMessages": [],
    "ResultMessage": "Successfully exported dictionaries from Prc service prc!\nExport file can be download from here: https://api.slamby.com/demo-api/files/345e1c79-dc78-427f-8ad1-facce75f6ae3.zip"
}
```



## Processes
There are long running tasks in the Slamby API. These requests are served in async way. These methods returns with `HTTP/1.1 202 ACCEPTED`and with a Process object.

> **Tip:** You can cancel a process anytime during its progress

### Get Process information
Get a process by its Id.

*Example REQUEST*
> [GET /api/Processes/`GUID`](#operation--api-Processes-get)

*Example RESPONSE*
> HTTP/1.1 200 OK
```JSON
{
    "Id": "958c1bdd-cd21-48f6-b9ec-c232271adec5",
    "Start": "2016-04-18T16:04:04.2156558Z",
    "End": "0001-01-01T00:00:00",
    "Percent": 0,
    "Description": "Exporting words from 1 tag(s) of dataset test...",
    "Status": "InProgress",
    "Type": "TagsExportWords",
    "ErrorMessages": [],
    "ResultMessage": null
}
```

### Cancel Process
Cancel the process. Only Process with status `InProgress` can be canceled. The method returns with `HTTP/1.1 202 ACCEPTED` because it can take time to cancel a process. You have to check periodically that whether the process status is already `Canceled`.

*Example REQUEST*
> [POST /api/Processes/`GUID`](#operation--api-Processes-Cancel-post)

*Example RESPONSE*
> HTTP/1.1 202 ACCEPTED

##### For the parameters explanation check the Process schema definition [here](#/definitions/Process)

## General


### Pagination
There are situations when your query results lots of data. In these cases the pagination can be handy.
You have to only provide an offset and a limit in the pagination object.
Optionally you can set a field which will be the base of the ordering and also the direction of the ordering (ascendig or descending). If you specify `-1` value for `Limit` then you will get all the elements in one result.

In the result (PaginatedList[Object]) there is an Items property which containing the requested elements (or the part of the requested elements). Also it provides the count of the items (this is equal or lesser than the limit property) and the total count of the requested items. Also it returns the same pagination object which was int the request.

>*Example REQUEST*
```json
{
    ...
    "Pagination" : {
        "Offset" : 0,
        "Limit": 10,
        "OrderDirection" : "Asc",
        "OrderByField" : "title"
    }
}
```

>*Example RESPONSE*
```json
{
    "Items": [
    {
        ...
    },
    {
        ...
    },
    ...
    ],
    "Pagination": {
    "Offset": 0,
    "Limit": 100,
    "OrderDirection": "Asc",
    "OrderByField": "title"
    },
    "Count": 10,
    "Total": 21
}
```

##### Check the Pagination schema definition [here](#/definitions/Pagination)
##### Check the PaginatedList[Object] schema definition [here](#/definitions/PaginatedList[Object])

### Status

*Example REQUEST*

> [GET /api/Status](#operation--api-Status-get))

*Example RESPONSE*
> HTTP/1.1 200 OK
```JSON
{
    "ProcessorCount": 4,
    "AvailableFreeSpace": 47895.53,
    "ApiVersion": "0.14.0",
    "CpuUsage": 0.6,
    "TotalMemory": 996.08,
    "FreeMemory": 36.3
}
```